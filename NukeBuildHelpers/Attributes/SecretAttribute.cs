using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SecretHelperAttribute(string secretVariableName, string? environmentVariableName = null) : Attribute
{
    public string SecretVariableName { get; } = secretVariableName;

    public string? EnvironmentVariableName { get; } = environmentVariableName;
}
