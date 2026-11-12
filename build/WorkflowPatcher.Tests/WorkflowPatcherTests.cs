using System.Text;
using NukeBuildHelpers.Build;

namespace NukeBuildHelpers.Build.Tests;

/// <summary>
/// Pins the post-generation patch applied to the workflow emitted by
/// NukeBuildHelpers: the publish job must get job-level NuGet Trusted
/// Publishing permissions, a SHA-pinned <c>NuGet/login</c> step, the
/// <c>NUKE_NUGET_AUTH_TOKEN</c> environment variable on the NUKE_RUN step,
/// and a fail-closed job-level push gate.
/// </summary>
public sealed class WorkflowPatcherTests
{
    private static readonly string UnpatchedWorkflow = ToLf("""
        name: Nuke CICD Pipeline
        jobs:
          PRE_SETUP:
            name: Pre Setup
            runs-on:
              labels: ubuntu-22.04
            steps:
            - id: NUKE_RUN
              name: Run Nuke PipelinePreSetup
              run: ./build.sh PipelinePreSetup
          NUKEBUILDHELPERSPUBLISH:
            name: Publish - nuke_build_helpers (NukeBuildHelpersPublish)
            runs-on: ${{ fromJson(needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_RUNS_ON) }}
            steps:
            - uses: actions/checkout@v6
              with:
                fetch-depth: 0
                persist-credentials: true
            - name: Cache Run
              uses: actions/cache@v5
              with:
                path: ./.nuke/temp/cache
                key: ${{ needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_CACHE_KEY }}
            - id: NUKE_RUN
              name: Run Nuke NukeBuildHelpersPublish
              run: ${{ needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_RUN_SCRIPT }} Run --args "NukeBuildHelpersPublish"
            - name: Upload common publish artifacts
              uses: actions/upload-artifact@v7
              with:
                name: publish___$common___NUKEBUILDHELPERSPUBLISH
            needs:
            - PRE_SETUP
            - NUKEBUILDHELPERSTEST1
            - NUKEBUILDHELPERSBUILD1
            if: "! failure() && ! cancelled() && needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_CONDITION == 'true' && needs.NUKEBUILDHELPERSTEST1.result == 'success' && needs.NUKEBUILDHELPERSBUILD1.result == 'success'"
            env:
              NUKE_PRE_SETUP: ${{ needs.PRE_SETUP.outputs.NUKE_PRE_SETUP }}
          POST_SETUP:
            name: Post Setup
            runs-on:
              labels: ubuntu-22.04
            steps:
            - name: Run Nuke PipelinePostSetup
              run: ./build.sh PipelinePostSetup
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          NUKE_NUGET_AUTH_TOKEN: ${{ secrets.NUGET_AUTH_TOKEN }}
          NUKE_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        """);

    private static readonly string PatchedWorkflow = ToLf("""
        name: Nuke CICD Pipeline
        jobs:
          PRE_SETUP:
            name: Pre Setup
            runs-on:
              labels: ubuntu-22.04
            steps:
            - id: NUKE_RUN
              name: Run Nuke PipelinePreSetup
              run: ./build.sh PipelinePreSetup
          NUKEBUILDHELPERSPUBLISH:
            name: Publish - nuke_build_helpers (NukeBuildHelpersPublish)
            runs-on: ${{ fromJson(needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_RUNS_ON) }}
            permissions:
              contents: read
              packages: write
              id-token: write
            steps:
            - uses: actions/checkout@v6
              with:
                fetch-depth: 0
                persist-credentials: true
            - name: Cache Run
              uses: actions/cache@v5
              with:
                path: ./.nuke/temp/cache
                key: ${{ needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_CACHE_KEY }}
            - id: nuget_login
              name: NuGet login (OIDC)
              uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
              with:
                user: Kiryuumaru
              if: github.event_name == 'push'
            - id: NUKE_RUN
              name: Run Nuke NukeBuildHelpersPublish
              run: ${{ needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_RUN_SCRIPT }} Run --args "NukeBuildHelpersPublish"
              env:
                NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}
            - name: Upload common publish artifacts
              uses: actions/upload-artifact@v7
              with:
                name: publish___$common___NUKEBUILDHELPERSPUBLISH
            needs:
            - PRE_SETUP
            - NUKEBUILDHELPERSTEST1
            - NUKEBUILDHELPERSBUILD1
            if: "github.event_name == 'push' && (! failure() && ! cancelled() && needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_NUKEBUILDHELPERSPUBLISH_CONDITION == 'true' && needs.NUKEBUILDHELPERSTEST1.result == 'success' && needs.NUKEBUILDHELPERSBUILD1.result == 'success')"
            env:
              NUKE_PRE_SETUP: ${{ needs.PRE_SETUP.outputs.NUKE_PRE_SETUP }}
          POST_SETUP:
            name: Post Setup
            runs-on:
              labels: ubuntu-22.04
            steps:
            - name: Run Nuke PipelinePostSetup
              run: ./build.sh PipelinePostSetup
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          NUKE_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        """);

    [Fact]
    public void Patch_AddsPermissionsLoginStepStepEnvAndJobGateToPublishJob()
    {
        var patched = WorkflowPatcher.Patch(UnpatchedWorkflow);

        Assert.Equal(PatchedWorkflow, patched);
    }

    [Fact]
    public void Patch_AppliedTwice_ReturnsSameString()
    {
        var once = WorkflowPatcher.Patch(UnpatchedWorkflow);

        var twice = WorkflowPatcher.Patch(once);

        Assert.Equal(PatchedWorkflow, once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Patch_MissingPublishJob_ThrowsNamingAnchor()
    {
        var yaml = ToLf("""
            jobs:
              PRE_SETUP:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_MissingStepsAnchor_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                name: Publish
                runs-on: ubuntu-22.04
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("steps:", exception.Message);
    }

    [Fact]
    public void Patch_MissingNukeRunStep_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - name: Run Nuke
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("NUKE_RUN", exception.Message);
    }

    [Fact]
    public void Patch_MissingRunLine_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  name: Run Nuke
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("run:", exception.Message);
        Assert.Contains("NUKE_RUN", exception.Message);
    }

    [Theory]
    [InlineData("|")]
    [InlineData(">")]
    [InlineData("|+")]
    [InlineData("|-")]
    [InlineData(">+")]
    [InlineData(">-")]
    public void Patch_BlockScalarRun_Throws(string indicator)
    {
        var yaml = ToLf($"""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: {indicator}
                    ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("run:", exception.Message);
        Assert.Contains("block scalar", exception.Message);
    }

    [Fact]
    public void Patch_EmptyRun_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run:
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("run:", exception.Message);
        Assert.Contains("single-line", exception.Message);
    }

    [Theory]
    [InlineData("# only a comment")]
    [InlineData("echo hi # trailing comment")]
    public void Patch_CommentedRun_Throws(string runValue)
    {
        var yaml = ToLf($"""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: {runValue}
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("run:", exception.Message);
        Assert.Contains("trailing comment", exception.Message);
    }

    [Fact]
    public void Patch_RunWithContinuationLine_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
                    --flag
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("run:", exception.Message);
        Assert.Contains("continuation", exception.Message);
    }

    [Fact]
    public void Patch_RunWithBlankAndCommentBeforeContinuation_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh

                  # folded into the plain scalar below
                    --flag
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("run:", exception.Message);
        Assert.Contains("continuation", exception.Message);
    }

    [Fact]
    public void Patch_MultiLineRunsOnMapping_InsertsPermissionsBeforeSteps()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on:
                  labels: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
                - name: Upload common publish artifacts
                  uses: actions/upload-artifact@v7
            """);

        var expected = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on:
                  labels: ubuntu-22.04
                if: "github.event_name == 'push' && (! failure())"
                permissions:
                  contents: read
                  packages: write
                  id-token: write
                steps:
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
                  with:
                    user: Kiryuumaru
                  if: github.event_name == 'push'
                - id: NUKE_RUN
                  run: ./build.sh
                  env:
                    NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}
                - name: Upload common publish artifacts
                  uses: actions/upload-artifact@v7
            """);

        Assert.Equal(expected, WorkflowPatcher.Patch(yaml));
    }

    [Fact]
    public void Patch_FoldedScalarRunsOn_InsertsPermissionsBeforeSteps()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: >-
                  ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
                - name: Upload common publish artifacts
                  uses: actions/upload-artifact@v7
            """);

        var expected = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: >-
                  ubuntu-22.04
                if: "github.event_name == 'push' && (! failure())"
                permissions:
                  contents: read
                  packages: write
                  id-token: write
                steps:
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
                  with:
                    user: Kiryuumaru
                  if: github.event_name == 'push'
                - id: NUKE_RUN
                  run: ./build.sh
                  env:
                    NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}
                - name: Upload common publish artifacts
                  uses: actions/upload-artifact@v7
            """);

        Assert.Equal(expected, WorkflowPatcher.Patch(yaml));
    }

    [Fact]
    public void Patch_PartialPermissions_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                permissions:
                  contents: read
                  id-token: write
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("partial or conflicting", exception.Message);
        Assert.Contains("permissions:", exception.Message);
    }

    [Fact]
    public void Patch_ExtraPermissions_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                permissions:
                  contents: read
                  packages: write
                  id-token: write
                  actions: read
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("extra 'permissions:' entries", exception.Message);
    }

    [Fact]
    public void Patch_InlinePermissions_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                permissions: read-all
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("inline or malformed", exception.Message);
    }

    [Fact]
    public void Patch_DuplicatePermissions_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                permissions:
                  contents: read
                  packages: write
                  id-token: write
                permissions:
                  contents: read
                  packages: write
                  id-token: write
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("more than one 'permissions:' key", exception.Message);
    }

    [Fact]
    public void Patch_LegacyV1LoginStep_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@v1
                  with:
                    user: Kiryuumaru
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("nuget_login", exception.Message);
        Assert.Contains("does not match the expected step", exception.Message);
    }

    [Fact]
    public void Patch_PullRequestLoginIf_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
                  with:
                    user: Kiryuumaru
                  if: github.event_name != 'pull_request'
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("nuget_login", exception.Message);
        Assert.Contains("does not match the expected step", exception.Message);
    }

    [Fact]
    public void Patch_DuplicateLoginStep_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
                  with:
                    user: Kiryuumaru
                  if: github.event_name == 'push'
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
                  with:
                    user: Kiryuumaru
                  if: github.event_name == 'push'
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("more than one 'nuget_login' step", exception.Message);
    }

    [Fact]
    public void Patch_LoginStepAfterNukeRun_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                permissions:
                  contents: read
                  packages: write
                  id-token: write
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
                  env:
                    NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}
                - id: nuget_login
                  name: NuGet login (OIDC)
                  uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
                  with:
                    user: Kiryuumaru
                  if: github.event_name == 'push'
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("must precede", exception.Message);
        Assert.Contains("nuget_login", exception.Message);
        Assert.Contains("NUKE_RUN", exception.Message);
    }

    [Fact]
    public void Patch_MultiplePublishJobs_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
              NUKEBUILDHELPERSECONDPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("Multiple publish jobs", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSECONDPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_UnexpectedPublishJobName_Throws()
    {
        var yaml = ToLf("""
            jobs:
              OTHERPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("OTHERPUBLISH", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_RemovesLegacyWorkflowLevelToken()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            env:
              GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
              NUKE_NUGET_AUTH_TOKEN: ${{ secrets.NUGET_AUTH_TOKEN }}
              NUKE_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
            """);

        var patched = WorkflowPatcher.Patch(yaml);

        Assert.DoesNotContain("secrets.NUGET_AUTH_TOKEN", patched);
        Assert.Contains(
            "env:\n  GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}\n  NUKE_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}",
            patched);
    }

    [Fact]
    public void Patch_LegacyTokenTopLevelFallback_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            env:
              GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
              NUKE_NUGET_AUTH_TOKEN: ${{ secrets.NUGET_AUTH_TOKEN || '' }}
              NUKE_GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("legacy", exception.Message);
        Assert.Contains("NUKE_NUGET_AUTH_TOKEN", exception.Message);
    }

    [Theory]
    [InlineData("NUKE_NUGET_AUTH_TOKEN: ${{ secrets.NUGET_AUTH_TOKEN }}")]
    [InlineData("NUKE_NUGET_AUTH_TOKEN: ${{ secrets.NUGET_AUTH_TOKEN || '' }}")]
    public void Patch_LegacyTokenAtJobLevel_Throws(string legacyLine)
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                env:
                  __LEGACY__
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """).Replace("__LEGACY__", legacyLine, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("legacy", exception.Message);
        Assert.Contains("NUKE_NUGET_AUTH_TOKEN", exception.Message);
    }

    [Fact]
    public void Patch_LegacyTokenAtStepLevel_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - name: Cache Run
                  uses: actions/cache@v5
                  env:
                    NUKE_NUGET_AUTH_TOKEN: ${{ secrets.NUGET_AUTH_TOKEN }}
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("legacy", exception.Message);
        Assert.Contains("NUKE_NUGET_AUTH_TOKEN", exception.Message);
    }

    [Theory]
    [InlineData("if: \"! failure()\"", "if: \"github.event_name == 'push' && (! failure())\"")]
    [InlineData("if: ! failure()", "if: \"github.event_name == 'push' && (! failure())\"")]
    [InlineData(
        "if: '! failure() && needs.PRE_SETUP.result == ''success'''",
        "if: \"github.event_name == 'push' && (! failure() && needs.PRE_SETUP.result == 'success')\"")]
    [InlineData(
        "if: needs.PRE_SETUP.result == 'success'",
        "if: \"github.event_name == 'push' && (needs.PRE_SETUP.result == 'success')\"")]
    [InlineData(
        "if: contains(github.ref, \"release\")",
        "if: 'github.event_name == ''push'' && (contains(github.ref, \"release\"))'")]
    [InlineData("if: ${{ ! failure() }}", "if: ${{ github.event_name == 'push' && (! failure()) }}")]
    [InlineData("if: \"${{ ! failure() }}\"", "if: ${{ github.event_name == 'push' && (! failure()) }}")]
    public void Patch_RewritableJobIf_PrependsPushGate(string ifLine, string expectedIfLine)
    {
        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal($"    {expectedIfLine}", jobIf);
    }

    [Theory]
    [InlineData("if: \"! failure()) || (true\"")]
    [InlineData("if: \"! failure()) || (needs.PRE_SETUP.result == 'success'\"")]
    [InlineData("if: \"${{ ! failure()) || (true }}\"")]
    [InlineData("if: '! failure()) || (\"true\")'")]
    public void Patch_RewriteWithEarlyClosingParen_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
        Assert.Contains("cannot be rewritten safely", exception.Message);
        Assert.Contains("balanced", exception.Message);
    }

    [Fact]
    public void Patch_RewriteWithOrInsideWrap_IsGatedAndIdempotent()
    {
        const string ifLine = "if: \"! failure() || true\"";

        var once = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = once.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal("    if: \"github.event_name == 'push' && (! failure() || true)\"", jobIf);

        var twice = WorkflowPatcher.Patch(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Patch_RewriteSingleQuotedWithDoubleQuote_IsGated()
    {
        const string ifLine = "if: '! failure() || \"true\"'";

        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal("    if: 'github.event_name == ''push'' && (! failure() || \"true\")'", jobIf);
    }

    [Fact]
    public void Patch_RewriteExpressionForm_IsGated()
    {
        const string ifLine = "if: ${{ ! failure() && needs.PRE_SETUP.result == 'success' }}";

        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal(
            "    if: ${{ github.event_name == 'push' && (! failure() && needs.PRE_SETUP.result == 'success') }}",
            jobIf);
    }

    [Theory]
    [InlineData("if: ${{ ! failure() }} trailing")]
    [InlineData("if: \"prefix ${{ ! failure() }}\"")]
    [InlineData("if: ${{ ! failure() }} ${{ true }}")]
    [InlineData("if: \"! failure() }}\"")]
    [InlineData("if: ${{ ! failure()")]
    [InlineData("if: ${{ github.event_name == 'push' && (! failure())")]
    [InlineData("if: ${{ github.event_name == 'push' && (! failure()) }} || true")]
    [InlineData("if: ${{ github.event_name == 'push' && (! failure()) }} trailing")]
    public void Patch_JobIfWithEmbeddedOrSurroundingExpressionText_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
        Assert.Contains("embedded or surrounding", exception.Message);
        Assert.Contains("not safely rewritable", exception.Message);
    }

    [Fact]
    public void Patch_RewriteSimpleExpressionForm_IsGatedAndIdempotent()
    {
        const string ifLine = "if: ${{ ! failure() }}";

        var once = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = once.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal("    if: ${{ github.event_name == 'push' && (! failure()) }}", jobIf);

        var twice = WorkflowPatcher.Patch(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Patch_RewriteQuotedSimpleExpressionForm_IsGatedAndIdempotent()
    {
        const string ifLine = "if: \"${{ ! failure() }}\"";

        var once = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = once.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal("    if: ${{ github.event_name == 'push' && (! failure()) }}", jobIf);

        var twice = WorkflowPatcher.Patch(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Patch_RewriteExpressionFormWithWhitespaceBeforeClose_IsGated()
    {
        const string ifLine = "if: ${{ ! failure()   }}";

        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal("    if: ${{ github.event_name == 'push' && (! failure()) }}", jobIf);
    }

    [Fact]
    public void Patch_AlreadyGatedExpressionJobIfWithNestedBalancedExpression_IsNotDoubleWrapped()
    {
        const string ifLine =
            "if: ${{ github.event_name == 'push' && ((! failure()) || (needs.PRE_SETUP.result == 'success')) }}";

        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal($"    {ifLine}", jobIf);
    }

    [Fact]
    public void Patch_RePatchWithWeakenedGate_ThrowsInsteadOfSilentNoOp()
    {
        var mutated = string.Join('\n', PatchedWorkflow.Split('\n').Select(line =>
            line.StartsWith("    if:", StringComparison.Ordinal)
                ? "    if: \"github.event_name == 'push' && (! failure()) || true\""
                : line));

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(mutated));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
        Assert.Contains("push gate", exception.Message);
        Assert.Contains("balanced", exception.Message);
    }

    [Theory]
    [InlineData("if: \"github.event_name == 'push' && (! failure())\"")]
    [InlineData("if: github.event_name == 'push' && (! failure())")]
    [InlineData("if: 'github.event_name == ''push'' && (! failure())'")]
    [InlineData("if: ${{ github.event_name == 'push' && (! failure()) }}")]
    public void Patch_AlreadyGatedJobIf_IsNotDoubleWrapped(string ifLine)
    {
        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal($"    {ifLine}", jobIf);
    }

    [Theory]
    [InlineData("if: \"github.event_name == 'push' && (! failure()) || true\"")]
    [InlineData("if: \"github.event_name == 'push' && (! failure()) || github.event_name == 'pull_request'\"")]
    [InlineData("if: \"github.event_name == 'push' && (! failure()) && github.event_name == 'pull_request'\"")]
    public void Patch_WeakenedAlreadyGatedJobIf_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("starts with the push gate", exception.Message);
        Assert.Contains("balanced", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Theory]
    [InlineData("if: \"github.event_name == 'push' && (! failure()\"")]
    [InlineData("if: \"github.event_name == 'push' && (! failure()))\"")]
    [InlineData("if: \"github.event_name == 'push' && ()\"")]
    [InlineData("if: \"github.event_name == 'push' && (! failure()) \"")]
    public void Patch_MalformedAlreadyGatedJobIf_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("starts with the push gate", exception.Message);
        Assert.Contains("balanced", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_AlreadyGatedJobIfWithNestedBalancedExpression_IsNotDoubleWrapped()
    {
        const string ifLine =
            "if: \"github.event_name == 'push' && ((! failure()) || (needs.PRE_SETUP.result == 'success'))\"";

        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal($"    {ifLine}", jobIf);
    }

    [Fact]
    public void Patch_AlreadyGatedExpressionJobIfWithWhitespaceBeforeClose_IsNotDoubleWrapped()
    {
        const string ifLine =
            "if: ${{ github.event_name == 'push' && (! failure())   }}";

        var patched = WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine));

        var jobIf = patched.Split('\n').Single(line => line.StartsWith("    if:"));
        Assert.Equal($"    {ifLine}", jobIf);
    }

    [Fact]
    public void Patch_EmptyJobIf_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf("if:")));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("empty", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_CommentOnlyJobIf_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf("if: # comment")));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("only a comment", exception.Message);
    }

    [Theory]
    [InlineData("if: \"\"")]
    [InlineData("if: ''")]
    public void Patch_EmptyQuotedJobIf_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("empty quoted scalar", exception.Message);
    }

    [Theory]
    [InlineData("if: \"! failure()")]
    [InlineData("if: '! failure()")]
    public void Patch_UnterminatedQuotedJobIf_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("unterminated", exception.Message);
    }

    [Fact]
    public void Patch_TrailingCommentJobIf_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf("if: ! failure() # comment")));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("trailing comment", exception.Message);
    }

    [Theory]
    [InlineData("|")]
    [InlineData("|-")]
    [InlineData("|+")]
    [InlineData(">")]
    [InlineData(">-")]
    [InlineData(">+")]
    public void Patch_BlockScalarJobIf_Throws(string indicator)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf($"if: {indicator}")));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("block scalar", exception.Message);
    }

    [Fact]
    public void Patch_MissingJobIf_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("job-level 'if:'", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_DuplicateJobIf_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                if: "! cancelled()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("more than one job-level 'if:' key", exception.Message);
    }

    [Fact]
    public void Patch_JobIfContinuationLine_Throws()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: ! failure()
                  && needs.PRE_SETUP.result == 'success'
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => WorkflowPatcher.Patch(yaml));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("continuation", exception.Message);
    }

    [Theory]
    [InlineData("if: \"github.event_name != 'pull_request' && (! failure())\"")]
    [InlineData("if: \"github.event_name == 'push' || true\"")]
    [InlineData("if: \"github.event_name == 'push' && ! failure()\"")]
    [InlineData("if: \"(! failure()) && github.event_name == 'push'\"")]
    [InlineData("if: \"github.event_name == 'pull_request'\"")]
    [InlineData("if: ${{ github.event_name != 'pull_request' && (! failure()) }}")]
    [InlineData("if: ${{ github.event_name == 'push' }}")]
    public void Patch_ConflictingJobIfGateTerm_Throws(string ifLine)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf(ifLine)));

        Assert.Contains("conflicting", exception.Message);
        Assert.Contains("github.event_name", exception.Message);
        Assert.Contains("NUKEBUILDHELPERSPUBLISH", exception.Message);
    }

    [Fact]
    public void Patch_EmptyExpressionJobIf_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf("if: ${{ }}")));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("expression is empty", exception.Message);
    }

    [Fact]
    public void Patch_ExpressionJobIfWithTrailingComment_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowPatcher.Patch(WorkflowWithJobIf("if: ${{ ! failure() # comment }}")));

        Assert.Contains("if:", exception.Message);
        Assert.Contains("trailing comment", exception.Message);
    }

    [Fact]
    public void Patch_RunStepAtEndOfFile_InsertsEnvOnSeparateLine()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh

            """);

        var patched = WorkflowPatcher.Patch(yaml);

        Assert.Contains(
            "      run: ./build.sh\n      env:\n        NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}",
            patched);
    }

    [Fact]
    public void Patch_RunStepAtEndOfFileWithoutTrailingNewline_InsertsEnvAndKeepsFileUnterminated()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var patched = WorkflowPatcher.Patch(yaml);

        Assert.Contains(
            "      run: ./build.sh\n      env:\n        NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}",
            patched);
        Assert.False(patched.EndsWith('\n'));
    }

    [Fact]
    public void Patch_RunStepAtEofWithoutTrailingNewline_Crlf_InsertsEnvAndKeepsFileUnterminated()
    {
        var yaml = ToCrlf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
            """);

        var patched = WorkflowPatcher.Patch(yaml);

        Assert.Contains(
            "      run: ./build.sh\r\n      env:\r\n        NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}",
            patched);
        Assert.DoesNotContain("\n", patched.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.False(patched.EndsWith('\n'));
        Assert.False(patched.EndsWith('\r'));
    }

    [Fact]
    public void Patch_ExistingEnvBlockAtEofWithoutTrailingNewline_InsertsTokenAndKeepsFileUnterminated()
    {
        var yaml = ToLf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
                  env:
                    EXISTING: value
            """);

        var patched = WorkflowPatcher.Patch(yaml);

        Assert.Contains(
            "      env:\n        EXISTING: value\n        NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}",
            patched);
        Assert.False(patched.EndsWith('\n'));
    }

    [Fact]
    public void Patch_ExistingEnvBlockAtEofWithoutTrailingNewline_Crlf_InsertsTokenAndKeepsFileUnterminated()
    {
        var yaml = ToCrlf("""
            jobs:
              NUKEBUILDHELPERSPUBLISH:
                runs-on: ubuntu-22.04
                if: "! failure()"
                steps:
                - id: NUKE_RUN
                  run: ./build.sh
                  env:
                    EXISTING: value
            """);

        var patched = WorkflowPatcher.Patch(yaml);

        Assert.Contains(
            "      env:\r\n        EXISTING: value\r\n        NUKE_NUGET_AUTH_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}",
            patched);
        Assert.DoesNotContain("\n", patched.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.False(patched.EndsWith('\n'));
        Assert.False(patched.EndsWith('\r'));
    }

    [Fact]
    public void Patch_CrlfInput_ProducesCrlfOutputWithSameContent()
    {
        var crlf = ToCrlf(UnpatchedWorkflow);

        var patched = WorkflowPatcher.Patch(crlf);

        Assert.Contains("\r\n", patched);
        Assert.DoesNotContain("\n", patched.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("\r", patched.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.Equal(PatchedWorkflow, patched.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void PatchFile_WritesOnlyWhenChangedAndIsIdempotentOnDisk()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "nuke-cicd.yml");

        try
        {
            File.WriteAllText(path, UnpatchedWorkflow);

            WorkflowPatcher.PatchFile(path);

            Assert.Equal(PatchedWorkflow, File.ReadAllText(path));

            var marker = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, marker);

            WorkflowPatcher.PatchFile(path);

            Assert.Equal(marker, File.GetLastWriteTimeUtc(path));
            Assert.Equal(PatchedWorkflow, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PatchFile_PreservesUtf8BomAndDoesNotRewriteUnchangedFile()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "nuke-cicd.yml");

        try
        {
            var content = Encoding.UTF8.GetBytes(UnpatchedWorkflow);
            var bytes = new byte[3 + content.Length];
            bytes[0] = 0xEF;
            bytes[1] = 0xBB;
            bytes[2] = 0xBF;
            content.CopyTo(bytes, 3);
            File.WriteAllBytes(path, bytes);

            WorkflowPatcher.PatchFile(path);

            var patchedBytes = File.ReadAllBytes(path);
            Assert.True(patchedBytes.Length >= 3);
            Assert.Equal(0xEF, patchedBytes[0]);
            Assert.Equal(0xBB, patchedBytes[1]);
            Assert.Equal(0xBF, patchedBytes[2]);
            Assert.Equal(PatchedWorkflow, Encoding.UTF8.GetString(patchedBytes, 3, patchedBytes.Length - 3));

            var marker = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, marker);

            WorkflowPatcher.PatchFile(path);

            Assert.Equal(marker, File.GetLastWriteTimeUtc(path));
            Assert.Equal(patchedBytes, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Patch_CommittedWorkflow_IsAlreadyFullyPatched()
    {
        var path = CommittedWorkflowLocator.Find();
        if (path is null)
        {
            throw new InvalidOperationException(
                "Could not locate '.github/workflows/nuke-cicd.yml' above the test assembly directory; " +
                "the committed-workflow guard cannot run.");
        }

        var yaml = File.ReadAllText(path);

        Assert.Equal(yaml, WorkflowPatcher.Patch(yaml));
    }

    private static string WorkflowWithJobIf(string ifLine) => ToLf($"""
        jobs:
          NUKEBUILDHELPERSPUBLISH:
            runs-on: ubuntu-22.04
            {ifLine}
            steps:
            - id: NUKE_RUN
              run: ./build.sh
            - name: Upload common publish artifacts
              uses: actions/upload-artifact@v7
        """);

    /// <summary>
    /// Normalizes source line endings so fixtures read identically regardless
    /// of the checkout's line-ending configuration.
    /// </summary>
    private static string ToLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Produces a CRLF fixture from any checkout, normalizing first so a CRLF
    /// checkout cannot yield doubled carriage returns.
    /// </summary>
    private static string ToCrlf(string value) =>
        ToLf(value).Replace("\n", "\r\n", StringComparison.Ordinal);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "WorkflowPatcherTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

internal static class CommittedWorkflowLocator
{
    public static string? Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".github", "workflows", "nuke-cicd.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
