namespace NukeBuildHelpers.Enums;

[Flags]
public enum RunTestType
{
    None = 0b00,
    Local = 0b01,
    Target = 0b10,
    All = 0b11,
}
