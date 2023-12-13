using NukeBuildHelpers.SourceGenerators.ComponentModel.Models;
using NukeBuildHelpers.SourceGenerators.Extensions;
using NukeBuildHelpers.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using static NukeBuildHelpers.SourceGenerators.Diagnostics.DiagnosticDescriptors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NukeBuildHelpers.SourceGenerators
{
    public abstract partial class TransitiveMembersGenerator<TInfo> : IIncrementalGenerator
    {
        public ClassDeclarationSyntax ClassDeclaration { get; }

        private readonly string attributeType;

        private readonly IEqualityComparer<TInfo> comparer;

        private readonly ImmutableArray<MemberDeclarationSyntax> sealedMemberDeclarations;

        private readonly ImmutableArray<MemberDeclarationSyntax> nonSealedMemberDeclarations;

        public TransitiveMembersGenerator(string attributeType, IEqualityComparer<TInfo>? comparer = null)
        {
            this.attributeType = attributeType;
            this.comparer = comparer ?? EqualityComparer<TInfo>.Default;

            string attributeTypeName = attributeType.Split('.').Last();
            string filename = $"NukeBuildHelpers.SourceGenerators.EmbeddedResources.{attributeTypeName.Replace("Attribute", string.Empty)}.cs";

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename);
            using StreamReader reader = new(stream);

            string source = reader.ReadToEnd();
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            ClassDeclaration = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

            ImmutableArray<MemberDeclarationSyntax> annotatedMemberDeclarations = ClassDeclaration.Members.ToImmutableArray().Select(member =>
            {
                // [GeneratedCode] is always present
                member =
                    member
                    .WithoutLeadingTrivia()
                    .AddAttributeLists(AttributeList(SingletonSeparatedList(
                        Attribute(IdentifierName($"global::System.CodeDom.Compiler.GeneratedCode"))
                        .AddArgumentListArguments(
                            AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(GetType().FullName))),
                            AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(GetType().Assembly.GetName().Version.ToString())))))))
                    .WithLeadingTrivia(member.GetLeadingTrivia());

                // [ExcludeFromCodeCoverage] is not supported on interfaces and fields
                if (member.Kind() is not SyntaxKind.InterfaceDeclaration and not SyntaxKind.FieldDeclaration)
                {
                    member = member.AddAttributeLists(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage")))));
                }

                return member;
            }).ToImmutableArray();

            // If the target class is sealed, make protected members private and remove the virtual modifier
            sealedMemberDeclarations = annotatedMemberDeclarations.Select(static member =>
            {
                // Constructors become public for sealed types
                if (member is ConstructorDeclarationSyntax)
                {
                    return member.ReplaceModifier(SyntaxKind.ProtectedKeyword, SyntaxKind.PublicKeyword);
                }

                // Other members become private
                return
                    member
                    .ReplaceModifier(SyntaxKind.ProtectedKeyword, SyntaxKind.PrivateKeyword)
                    .RemoveModifier(SyntaxKind.VirtualKeyword);
            }).ToImmutableArray();

            nonSealedMemberDeclarations = annotatedMemberDeclarations;
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var appEntries = context.AdditionalTextsProvider
                .Where(a => a.Path.EndsWith("appentry.json"))
                .Select((a, c) => (a.Path, a.GetText(c)!.ToString()));

            // Get all class declarations
            IncrementalValuesProvider<INamedTypeSymbol> typeSymbols =
                context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                    static (context, _) => (INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node)!);

            // Filter the types with the target attribute
            IncrementalValuesProvider<(INamedTypeSymbol Symbol, AttributeData AttributeData)> typeSymbolsWithAttributeData =
                typeSymbols
                .Select((item, _) => (
                    Symbol: item,
                    Attribute: item.GetAttributes().FirstOrDefault(a => a.AttributeClass?.HasFullyQualifiedName(attributeType) == true)))
                .Where(static item => item.Attribute is not null)!;

            // Transform the input data
            IncrementalValuesProvider<(INamedTypeSymbol Symbol, TInfo Info)> typeSymbolsWithInfo = GetInfo(context, typeSymbolsWithAttributeData);

            // Filter by language version
            context.FilterWithLanguageVersion(ref typeSymbolsWithInfo, LanguageVersion.CSharp8, UnsupportedCSharpLanguageVersionError);

            // Gather all generation info, and any diagnostics
            IncrementalValuesProvider<Result<(HierarchyInfo Hierarchy, bool IsSealed, TInfo Info)>> generationInfoWithErrors =
                typeSymbolsWithInfo.Select((item, _) =>
                {
                    if (ValidateTargetType(item.Symbol, item.Info, out ImmutableArray<Diagnostic> diagnostics))
                    {
                        return new Result<(HierarchyInfo, bool, TInfo)>(
                            (HierarchyInfo.From(item.Symbol), item.Symbol.IsSealed, item.Info),
                            ImmutableArray<Diagnostic>.Empty);
                    }

                    return new Result<(HierarchyInfo, bool, TInfo)>(default, diagnostics);
                });

            // Emit the diagnostic, if needed
            context.ReportDiagnostics(generationInfoWithErrors.Select(static (item, _) => item.Errors));

            // Get the filtered sequence to enable caching
            IncrementalValuesProvider<(HierarchyInfo Hierarchy, bool IsSealed, TInfo Info)> generationInfo =
                generationInfoWithErrors
                .Where(static item => item.Errors.IsEmpty)
                .Select(static (item, _) => item.Value)
                .WithComparers(HierarchyInfo.Comparer.Default, EqualityComparer<bool>.Default, comparer);
            
            // Generate the required members
            context.RegisterSourceOutput(generationInfo, (context, item) =>
            {
                ImmutableArray<MemberDeclarationSyntax> sourceMemberDeclarations = item.IsSealed ? sealedMemberDeclarations : nonSealedMemberDeclarations;
                ImmutableArray<MemberDeclarationSyntax> filteredMemberDeclarations = FilterDeclaredMembers(item.Info, sourceMemberDeclarations);
                CompilationUnitSyntax compilationUnit = GetCompilationUnit(context, item.Info, item.Hierarchy, item.IsSealed, filteredMemberDeclarations);

                context.AddSource(item.Hierarchy.FilenameHint, compilationUnit.ToFullString());
            });
        }

        protected abstract bool ValidateTargetType(INamedTypeSymbol typeSymbol, TInfo info, out ImmutableArray<Diagnostic> diagnostics);

        protected abstract IncrementalValuesProvider<(INamedTypeSymbol Symbol, TInfo Info)> GetInfo(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<(INamedTypeSymbol Symbol, AttributeData AttributeData)> source);

        protected abstract ImmutableArray<MemberDeclarationSyntax> FilterDeclaredMembers(TInfo info, ImmutableArray<MemberDeclarationSyntax> memberDeclarations);

        protected abstract CompilationUnitSyntax GetCompilationUnit(SourceProductionContext sourceProductionContext, TInfo info, HierarchyInfo hierarchyInfo, bool isSealed, ImmutableArray<MemberDeclarationSyntax> memberDeclarations);
    }
}
