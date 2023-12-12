using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Attributes;

/// <summary>
/// Contains all methods for performing proper <see cref="IAsyncDisposable"/> operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NukeBuildCommonHelpersAttribute : Attribute
{
}
