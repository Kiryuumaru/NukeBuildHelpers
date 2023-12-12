using NukeBuildHelpers.SourceGenerators.ComponentModel.Models;
using NukeBuildHelpers.SourceGenerators.Diagnostics;
using NukeBuildHelpers.SourceGenerators.Extensions;
using NukeBuildHelpers.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using static NukeBuildHelpers.SourceGenerators.Diagnostics.DiagnosticDescriptors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static System.Net.Mime.MediaTypeNames;

namespace NukeBuildHelpers.SourceGenerators
{
    [Generator(LanguageNames.CSharp)]
    internal sealed partial class NukeBuildCommonHelpersGenerator : TransitiveMembersGenerator<NukeBuildCommonHelpersInfo>
    {
        public NukeBuildCommonHelpersGenerator()
            : base("global::NukeBuildHelpers.Attributes.NukeBuildCommonHelpersAttribute")
        {

        }

        protected override IncrementalValuesProvider<(INamedTypeSymbol Symbol, NukeBuildCommonHelpersInfo Info)> GetInfo(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<(INamedTypeSymbol Symbol, AttributeData AttributeData)> source)
        {
            static NukeBuildCommonHelpersInfo GetInfo(INamedTypeSymbol typeSymbol, AttributeData attributeData)
            {
                string typeName = typeSymbol.Name;
                bool hasImplementedINukeBuild = typeSymbol.AllInterfaces.Any(i => i.HasFullyQualifiedName("global::Nuke.Common.INukeBuild"));

                return new(typeName, hasImplementedINukeBuild);
            }

            return
                source
                .Select(static (item, _) => (item.Symbol, GetInfo(item.Symbol, item.AttributeData)));
        }

        protected override bool ValidateTargetType(INamedTypeSymbol typeSymbol, NukeBuildCommonHelpersInfo info, out ImmutableArray<Diagnostic> diagnostics)
        {
            ImmutableArray<Diagnostic>.Builder builder = ImmutableArray.CreateBuilder<Diagnostic>();

            // Check if the type uses [NukeBuildHelpersAttribute] already (in the type hierarchy too)
            if (typeSymbol.InheritsAttributeWithFullyQualifiedName("global::NukeBuildHelpers.Attributes.NukeBuildCommonHelpersAttribute"))
            {
                builder.Add(InvalidAttributeCombinationForNukeBuildHelpersAttributeError, typeSymbol, typeSymbol);

                diagnostics = builder.ToImmutable();

                return false;
            }

            diagnostics = builder.ToImmutable();

            return true;
        }

        protected override ImmutableArray<MemberDeclarationSyntax> FilterDeclaredMembers(NukeBuildCommonHelpersInfo info, ImmutableArray<MemberDeclarationSyntax> memberDeclarations)
        {
            ImmutableArray<MemberDeclarationSyntax>.Builder builder = ImmutableArray.CreateBuilder<MemberDeclarationSyntax>();

            static MemberDeclarationSyntax? FixupFilteredMemberDeclaration(MemberDeclarationSyntax member)
            {
                return member;
            }

            // Generate
            foreach (MemberDeclarationSyntax member in memberDeclarations.Where(static member => member is not DestructorDeclarationSyntax))
            {
                MemberDeclarationSyntax? syntax = FixupFilteredMemberDeclaration(member);
                if (syntax != null)
                {
                    builder.Add(syntax);
                }
            }

            return builder.ToImmutable();
        }

        protected override CompilationUnitSyntax GetCompilationUnit(SourceProductionContext sourceProductionContext, NukeBuildCommonHelpersInfo info, HierarchyInfo hierarchyInfo, bool isSealed, ImmutableArray<MemberDeclarationSyntax> memberDeclarations)
        {
            if (info.HasImplementedINukeBuild)
            {
                return hierarchyInfo.GetCompilationUnit(memberDeclarations);
            }
            else
            {
                return hierarchyInfo.GetCompilationUnit(memberDeclarations, ClassDeclaration.BaseList);
            }
        }
    }
}
