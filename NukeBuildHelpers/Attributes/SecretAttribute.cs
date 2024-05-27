namespace NukeBuildHelpers.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SecretAttribute(string secretVariableName, string? environmentVariableName = null) : Attribute
{
    public string SecretVariableName { get; } = secretVariableName;

    public string? EnvironmentVariableName { get; } = environmentVariableName;
}
