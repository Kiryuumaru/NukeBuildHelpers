using System.Text.Json;

namespace NukeBuildHelpers.Common;

internal static class JsonExtension
{
    public static readonly JsonSerializerOptions SnakeCaseNamingOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static readonly JsonSerializerOptions SnakeCaseNamingOptionIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions OptionIndented = new()
    {
        WriteIndented = true
    };

}
