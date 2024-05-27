using NukeBuildHelpers;

partial class Build : BaseNukeBuildHelpers
{
    public static int Main () => Execute<Build>(x => x.Version);

    public override string[] EnvironmentBranches { get; } = [ "prerelease", "main" ];

    public override string MainEnvironmentBranch { get; } = "main";
}
