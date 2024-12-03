using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Attributes;

/// <summary>
/// Contains all methods for performing proper <see cref="IDisposable"/> operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NukeBuildHelpersAttribute : Attribute
{
}
