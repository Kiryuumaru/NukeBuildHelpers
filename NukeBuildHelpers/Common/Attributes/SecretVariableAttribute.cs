namespace NukeBuildHelpers.Common.Attributes;

/// <summary>
/// Attribute to mark a property or field as a secret variable.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SecretVariableAttribute(string secretVariableName, string? environmentVariableName = null) : Attribute
{
    /// <summary>
    /// Gets the name of the secret variable.
    /// </summary>
    public string SecretVariableName { get; } = secretVariableName;

    /// <summary>
    /// Gets the name of the environment variable corresponding to the secret variable.
    /// </summary>
    public string? EnvironmentVariableName { get; } = environmentVariableName;
}
