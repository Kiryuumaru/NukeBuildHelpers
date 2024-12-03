// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#pragma warning disable IDE0090 // Use 'new DiagnosticDescriptor(...)'

namespace NukeBuildHelpers.SourceGenerators.Diagnostics;

/// <summary>
/// A container for all <see cref="DiagnosticDescriptor"/> instances for errors reported by analyzers in this project.
/// </summary>
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnsupportedCSharpLanguageVersionError = new DiagnosticDescriptor(
        id: "NBH0001",
        title: "Unsupported C# language version",
        messageFormat: "The source generator features from the MVVM Toolkit require consuming projects to set the C# language version to at least C# 8.0",
        category: typeof(CSharpParseOptions).FullName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source generator features from the MVVM Toolkit require consuming projects to set the C# language version to at least C# 8.0. Make sure to add <LangVersion>8.0</LangVersion> (or above) to your .csproj file.",
        helpLinkUri: "https://github.com/Kiryuumaru/NukeBuildHelpers");

    public static readonly DiagnosticDescriptor InvalidAttributeCombinationForDisposableAttributeError = new DiagnosticDescriptor(
        id: "NBH0002",
        title: "Invalid target type for [Disposable]",
        messageFormat: "Cannot apply [Disposable] to type {0}, as it already has this attribute or [Disposable] applied to it (including base types)",
        category: typeof(NukeBuildHelpersGenerator).FullName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Cannot apply [Disposable] to a type that already has this attribute or [Disposable] applied to it (including base types).",
        helpLinkUri: "https://github.com/Kiryuumaru/DisposableHelpers");
}
