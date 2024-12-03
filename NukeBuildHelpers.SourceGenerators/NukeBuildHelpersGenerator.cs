using NukeBuildHelpers.SourceGenerators.Diagnostics;
using NukeBuildHelpers.SourceGenerators.Extensions;
using NukeBuildHelpers.SourceGenerators.Helpers;
using NukeBuildHelpers.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NukeBuildHelpers.SourceGenerators.Diagnostics.DiagnosticDescriptors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NukeBuildHelpers.SourceGenerators
{
    //[Generator(LanguageNames.CSharp)]
    internal sealed partial class NukeBuildHelpersGenerator : TransitiveMembersGenerator<NukeBuildHelpersInfo>
    {
        public NukeBuildHelpersGenerator()
            : base("global::NukeBuildHelpers.Attributes.NukeBuildHelpersAttribute")
        {

        }

        protected override IncrementalValuesProvider<(INamedTypeSymbol Symbol, NukeBuildHelpersInfo Info)> GetInfo(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<(INamedTypeSymbol Symbol, AttributeData AttributeData)> source)
        {
            static NukeBuildHelpersInfo GetInfo(INamedTypeSymbol typeSymbol, AttributeData attributeData)
            {
                string typeName = typeSymbol.Name;
                bool hasExplicitDestructors = typeSymbol.GetMembers().Any(m => m is IMethodSymbol symbol && symbol.MethodKind == MethodKind.Destructor);

                bool hasImplementedIDisposable = typeSymbol.AllInterfaces.Any(i => i.HasFullyQualifiedName("global::System.IDisposable"));
                bool hasImplementedIAsyncDisposable = typeSymbol.AllInterfaces.Any(i => i.HasFullyQualifiedName("global::System.IAsyncDisposable"));

                (IMethodSymbol symbol, bool fromDerived)? disposeMethod = null;
                (IMethodSymbol symbol, bool fromDerived)? disposeBoolMethod = null;
                (IMethodSymbol symbol, bool fromDerived)? disposeAsyncMethod = null;
                (IMethodSymbol symbol, bool fromDerived)? disposeAsyncBoolMethod = null;

                bool fromDerived = false;
                var currentTypeSymbol = typeSymbol;
                while (currentTypeSymbol != null)
                {
                    if (disposeMethod == null &&
                        currentTypeSymbol.GetMembers().FirstOrDefault(i =>
                            i is IMethodSymbol symbol &&
                            symbol.Name == "Dispose" &&
                            symbol.Parameters.Length == 0) is IMethodSymbol dm)
                    {
                        disposeMethod = (dm, fromDerived);
                    }
                    if (disposeBoolMethod == null &&
                        currentTypeSymbol.GetMembers().FirstOrDefault(i =>
                            i is IMethodSymbol symbol &&
                            symbol.Name == "Dispose" &&
                            symbol.Parameters.Length == 1 &&
                            symbol.Parameters[0].Type.Name == typeof(bool).Name) is IMethodSymbol dbm)
                    {
                        disposeBoolMethod = (dbm, fromDerived);
                    }
                    if (disposeAsyncMethod == null &&
                        currentTypeSymbol.GetMembers().FirstOrDefault(i =>
                            i is IMethodSymbol symbol &&
                            symbol.Name == "DisposeAsync" &&
                            symbol.Parameters.Length == 0) is IMethodSymbol dam)
                    {
                        disposeAsyncMethod = (dam, fromDerived);
                    }
                    if (disposeAsyncBoolMethod == null &&
                        currentTypeSymbol.GetMembers().FirstOrDefault(i =>
                            i is IMethodSymbol symbol &&
                            symbol.Name == "DisposeAsync" &&
                            symbol.Parameters.Length == 1 &&
                            symbol.Parameters[0].Type.Name == typeof(bool).Name) is IMethodSymbol dabm)
                    {
                        disposeAsyncBoolMethod = (dabm, fromDerived);
                    }

                    currentTypeSymbol = currentTypeSymbol.BaseType;

                    fromDerived = true;
                }

                return new(
                    typeName,
                    hasExplicitDestructors,
                    hasImplementedIDisposable,
                    hasImplementedIAsyncDisposable,
                    disposeMethod,
                    disposeAsyncMethod,
                    disposeBoolMethod,
                    disposeAsyncBoolMethod);
            }

            return
                source
                .Select(static (item, _) => (item.Symbol, GetInfo(item.Symbol, item.AttributeData)));
        }

        protected override bool ValidateTargetType(INamedTypeSymbol typeSymbol, NukeBuildHelpersInfo info, out ImmutableArray<Diagnostic> diagnostics)
        {
            ImmutableArray<Diagnostic>.Builder builder = ImmutableArray.CreateBuilder<Diagnostic>();

            // Check if the type uses [DisposableAttribute] already (in the type hierarchy too)
            if (typeSymbol.InheritsAttributeWithFullyQualifiedName("global::NukeBuildHelpers.Attributes.DisposableAttribute"))
            {
                builder.Add(InvalidAttributeCombinationForDisposableAttributeError, typeSymbol, typeSymbol);
            }

            //// Check if the type uses implemented void Dispose() and Dispose(bool) directly
            //if (info.DisposeMethod != null &&
            //    !info.DisposeMethod.Value.fromDerived &&
            //    info.DisposeBoolMethod != null &&
            //    !info.DisposeBoolMethod.Value.fromDerived)
            //{
            //    builder.Add(TargetHasDirectDisposeImplementationError, typeSymbol, typeSymbol);
            //}

            //// Check if the type uses implemented void DisposeAsync() and DisposeAsync(bool) directly
            //if (info.DisposeAsyncMethod != null &&
            //    !info.DisposeAsyncMethod.Value.fromDerived &&
            //    info.DisposeAsyncBoolMethod != null &&
            //    !info.DisposeAsyncBoolMethod.Value.fromDerived)
            //{
            //    builder.Add(TargetHasDirectDisposeAsyncImplementationError, typeSymbol, typeSymbol);
            //}

            //// Is base already disposable
            //if (info.DisposeMethod != null && info.DisposeMethod.Value.fromDerived)
            //{
            //    // Check if the type uses implemented void Dispose() or void Dispose(bool) but neither overridable
            //    if (!info.DisposeMethod.Value.symbol.IsVirtual && !info.DisposeMethod.Value.symbol.IsOverride &&
            //        (info.DisposeBoolMethod == null || (!info.DisposeBoolMethod.Value.symbol.IsVirtual && !info.DisposeBoolMethod.Value.symbol.IsOverride)))
            //    {
            //        builder.Add(TargetBaseNoOverridableDisposeMethodError, typeSymbol, typeSymbol);
            //    }
            //}

            //// Is base already disposable async
            //if (info.DisposeAsyncMethod != null && info.DisposeAsyncMethod.Value.fromDerived)
            //{
            //    // Check if the type uses implemented ValueTask DisposeAsync() or ValueTask DisposeAsync(bool) but neither overridable
            //    if (!info.DisposeAsyncMethod.Value.symbol.IsVirtual && !info.DisposeAsyncMethod.Value.symbol.IsOverride &&
            //        (info.DisposeAsyncBoolMethod == null || (!info.DisposeAsyncBoolMethod.Value.symbol.IsVirtual && !info.DisposeAsyncBoolMethod.Value.symbol.IsOverride)))
            //    {
            //        builder.Add(TargetBaseNoOverridableDisposeAsyncMethodError, typeSymbol, typeSymbol);
            //    }
            //}

            diagnostics = builder.ToImmutable();

            return diagnostics.Length == 0;
        }

        protected override ImmutableArray<MemberDeclarationSyntax> FilterDeclaredMembers(NukeBuildHelpersInfo info, ImmutableArray<MemberDeclarationSyntax> memberDeclarations)
        {
            ImmutableArray<MemberDeclarationSyntax>.Builder builder = ImmutableArray.CreateBuilder<MemberDeclarationSyntax>();

            // If the target type has no destructors, generate destructors.
            if (!info.HasExplicitDestructors)
            {
                foreach (DestructorDeclarationSyntax ctor in memberDeclarations.OfType<DestructorDeclarationSyntax>())
                {
                    string text = ctor.NormalizeWhitespace().ToFullString();
                    string replaced = text.Replace("~Disposable", $"~{info.TypeName}");

                    builder.Add((DestructorDeclarationSyntax)ParseMemberDeclaration(replaced)!);
                }
            }

            bool hasDirectDisposeMethod = info.DisposeMethod != null &&
                !info.DisposeMethod.Value.fromDerived;
            bool hasDirectDisposeBoolMethod = info.DisposeBoolMethod != null &&
                !info.DisposeBoolMethod.Value.fromDerived;
            bool hasDirectDisposeAsyncMethod = info.DisposeAsyncMethod != null &&
                !info.DisposeAsyncMethod.Value.fromDerived;
            bool hasDirectDisposeAsyncBoolMethod = info.DisposeAsyncBoolMethod != null &&
                !info.DisposeAsyncBoolMethod.Value.fromDerived;

            bool hasDerivedDisposeMethod = info.DisposeMethod != null &&
                info.DisposeMethod.Value.fromDerived;
            bool hasDerivedDisposeBoolMethod = info.DisposeBoolMethod != null &&
                info.DisposeBoolMethod.Value.fromDerived;
            bool hasDerivedDisposeAsyncMethod = info.DisposeAsyncMethod != null &&
                info.DisposeAsyncMethod.Value.fromDerived;
            bool hasDerivedDisposeAsyncBoolMethod = info.DisposeAsyncBoolMethod != null &&
                info.DisposeAsyncBoolMethod.Value.fromDerived;

            bool overridableDisposeMethod = info.DisposeMethod != null &&
                info.DisposeMethod.Value.fromDerived &&
                (info.DisposeMethod.Value.symbol.IsVirtual || info.DisposeMethod.Value.symbol.IsOverride);
            bool overridableDisposeBoolMethod = info.DisposeBoolMethod != null &&
                info.DisposeBoolMethod.Value.fromDerived &&
                (info.DisposeBoolMethod.Value.symbol.IsVirtual || info.DisposeBoolMethod.Value.symbol.IsOverride);
            bool overridableDisposeAsyncMethod = info.DisposeAsyncMethod != null &&
                info.DisposeAsyncMethod.Value.fromDerived &&
                (info.DisposeAsyncMethod.Value.symbol.IsVirtual || info.DisposeAsyncMethod.Value.symbol.IsOverride);
            bool overridableDisposeAsyncBoolMethod = info.DisposeAsyncBoolMethod != null &&
                info.DisposeAsyncBoolMethod.Value.fromDerived &&
                (info.DisposeAsyncBoolMethod.Value.symbol.IsVirtual || info.DisposeAsyncBoolMethod.Value.symbol.IsOverride);

            MemberDeclarationSyntax? FixupFilteredMemberDeclaration(MemberDeclarationSyntax member)
            {
                if (member is MethodDeclarationSyntax methodDeclarationSyntax)
                {
                    // Normal Dispose() if the target has no derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "Dispose_Normal" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 0)
                    {
                        if (!hasDirectDisposeMethod && !hasDerivedDisposeMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("Dispose"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Normal Dispose(bool) if the target has no derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "Dispose_Normal" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 1 &&
                        methodDeclarationSyntax.ParameterList.Parameters[0].Type?.ToString() == "bool")
                    {
                        if (!hasDirectDisposeBoolMethod && !hasDerivedDisposeBoolMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("Dispose"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Normal DisposeAsync() if the target has no derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "DisposeAsync_Normal" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 0)
                    {
                        if (!hasDirectDisposeAsyncMethod && !hasDerivedDisposeAsyncMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("DisposeAsync"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Normal DisposeAsync(bool) if the target has no derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "DisposeAsync_Normal" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 1 &&
                        methodDeclarationSyntax.ParameterList.Parameters[0].Type?.ToString() == "bool")
                    {
                        if (!hasDirectDisposeAsyncBoolMethod && !hasDerivedDisposeAsyncBoolMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("DisposeAsync"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Override Dispose() if the target has derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "Dispose_Override" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 0)
                    {
                        if (!hasDirectDisposeMethod && hasDerivedDisposeMethod && overridableDisposeMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("Dispose"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Override Dispose(bool) if the target has derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "Dispose_Override" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 1 &&
                        methodDeclarationSyntax.ParameterList.Parameters[0].Type?.ToString() == "bool")
                    {
                        if (!hasDirectDisposeBoolMethod && hasDerivedDisposeBoolMethod && overridableDisposeBoolMethod && !hasDirectDisposeMethod && hasDerivedDisposeMethod && overridableDisposeMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("Dispose"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Override Dispose(bool) if the target has derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "Dispose_OverrideCross" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 1 &&
                        methodDeclarationSyntax.ParameterList.Parameters[0].Type?.ToString() == "bool")
                    {
                        if (!hasDirectDisposeBoolMethod && hasDerivedDisposeBoolMethod && overridableDisposeBoolMethod && (hasDirectDisposeMethod || !hasDerivedDisposeMethod || !overridableDisposeMethod))
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("Dispose"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Override DisposeAsync() if the target has derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "DisposeAsync_Override" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 0)
                    {
                        if (!hasDirectDisposeAsyncMethod && hasDerivedDisposeAsyncMethod && overridableDisposeAsyncMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("DisposeAsync"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Override DisposeAsync(bool) if the target has derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "DisposeAsync_Override" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 1 &&
                        methodDeclarationSyntax.ParameterList.Parameters[0].Type?.ToString() == "bool")
                    {
                        if (!hasDirectDisposeAsyncBoolMethod && hasDerivedDisposeAsyncBoolMethod && overridableDisposeAsyncBoolMethod && !hasDirectDisposeAsyncMethod && hasDerivedDisposeAsyncMethod && overridableDisposeAsyncMethod)
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("DisposeAsync"));
                        }
                        else
                        {
                            return null;
                        }
                    }

                    // Override DisposeAsync(bool) if the target has derived disposable implementation
                    if (methodDeclarationSyntax.Identifier.ValueText == "DisposeAsync_OverrideCross" &&
                        methodDeclarationSyntax.ParameterList.Parameters.Count == 1 &&
                        methodDeclarationSyntax.ParameterList.Parameters[0].Type?.ToString() == "bool")
                    {
                        if (!hasDirectDisposeAsyncBoolMethod && hasDerivedDisposeAsyncBoolMethod && overridableDisposeAsyncBoolMethod && (hasDirectDisposeAsyncMethod || !hasDerivedDisposeAsyncMethod || !overridableDisposeAsyncMethod))
                        {
                            return methodDeclarationSyntax
                                .WithIdentifier(Identifier("DisposeAsync"));
                        }
                        else
                        {
                            return null;
                        }
                    }
                }

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

        protected override CompilationUnitSyntax GetCompilationUnit(SourceProductionContext sourceProductionContext, NukeBuildHelpersInfo info, HierarchyInfo hierarchyInfo, bool isSealed, ImmutableArray<MemberDeclarationSyntax> memberDeclarations)
        {
            if (info.HasImplementedIDisposable)
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
