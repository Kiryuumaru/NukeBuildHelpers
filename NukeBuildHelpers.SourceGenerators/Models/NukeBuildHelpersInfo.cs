// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;

namespace NukeBuildHelpers.SourceGenerators.Models;

public sealed record NukeBuildHelpersInfo(
    string TypeName,
    bool HasExplicitDestructors,
    bool HasImplementedIDisposable,
    bool HasImplementedIAsyncDisposable,
    (IMethodSymbol symbol, bool fromDerived)? DisposeMethod,
    (IMethodSymbol symbol, bool fromDerived)? DisposeAsyncMethod,
    (IMethodSymbol symbol, bool fromDerived)? DisposeBoolMethod,
    (IMethodSymbol symbol, bool fromDerived)? DisposeAsyncBoolMethod);
