# NukeBuildHelpers V9 Breaking Changes

This document outlines all breaking changes introduced in NukeBuildHelpers V9.0.0 and provides migration guidance for upgrading from V8.

## ?? CRITICAL BREAKING CHANGES

### 1. ?? **Context API Complete Refactoring**

**IMPACT: MASSIVE** - All Execute method examples require updates

#### **Removed Interfaces and Extension Methods**
All specific context interfaces and their extension methods have been **completely removed**:

- ? `IBumpContext`, `ICommitContext`, `ILocalContext`, `IPipelineContext`, `IPullRequestContext`, `IVersionedContext`
- ? `TryGetBumpContext()`, `TryGetPullRequestContext()`, `TryGetCommitContext()`, etc.
- ? `RunContextExtensions.cs` - All extension methods removed

#### **New Unified Context Model**
Introduced simplified `AppRunContext` with direct property access:

```csharp
// V8 (OLD) - REMOVED
if (context.TryGetBumpContext(out var bumpContext))
{
    version = bumpContext.AppVersion.Version.ToString();
    releaseNotes = bumpContext.AppVersion.ReleaseNotes;
}
else if (context.TryGetPullRequestContext(out var pullRequestContext))
{
    version = pullRequestContext.AppVersion.Version.ToString();
}

// V9 (NEW) - REQUIRED
var contextVersion = context.Apps.First().Value;
string version = contextVersion.AppVersion.Version.ToString();

if (contextVersion.BumpVersion != null)
{
    version = contextVersion.BumpVersion.Version.ToString();
    releaseNotes = contextVersion.BumpVersion.ReleaseNotes;
}
else if (contextVersion.PullRequestVersion != null)
{
    version = contextVersion.PullRequestVersion.Version.ToString();
}
```

#### **AppRunContext Properties**
```csharp
public class AppRunContext
{
    public string AppId { get; init; }
    public RunType RunType { get; init; }
    public AppVersion AppVersion { get; init; }
    public BumpReleaseVersion? BumpVersion { get; init; }
    public PullRequestReleaseVersion? PullRequestVersion { get; init; }
    public AbsolutePath OutputDirectory { get; }
    
    // Convenience Properties
    public bool IsBump { get; }
    public bool IsPullRequest { get; }
    public bool IsLocal { get; }
    public bool IsCommit { get; }
}
```

---

### 2. ?? **OutputDirectory Architecture Completely Changed**

**IMPACT: MASSIVE** - All OutputDirectory usage requires updates

#### **Removed Global OutputDirectory**
The static `OutputDirectory` property has been **completely removed** from `BaseNukeBuildHelpers`:

```csharp
// V8 (REMOVED) - No longer available
protected static readonly AbsolutePath OutputDirectory = CommonOutputDirectory / "main";

// This no longer works anywhere:
DotNetTasks.DotNetPack(_ => _
    .SetOutputDirectory(OutputDirectory / "main"));
```

#### **New App-Specific OutputDirectory**
Each app now has its own dedicated output directory accessible through context:

```csharp
// V9 (NEW) - App-specific output directories
var contextVersion = context.Apps.First().Value;
var appOutputDirectory = contextVersion.OutputDirectory;

// Path structure: CommonOutputDirectory / {appid} / "runtime"
// Example: .nuke/output/myapp/runtime/
```

#### **OutputDirectory Property Details**
```csharp
/// <summary>
/// Gets the output directory path where this application's build artifacts and files are stored during pipeline execution.
/// The path is constructed by combining the common runtime output directory with the lowercase application ID.
/// </summary>
public AbsolutePath OutputDirectory => BaseNukeBuildHelpers.CommonOutputDirectory / AppId.ToLowerInvariant() / "runtime";
```

#### **Migration Pattern**
```csharp
// V8 (OLD) - Global OutputDirectory
BuildEntry MyBuild => _ => _
    .Execute(context => {
        DotNetTasks.DotNetPack(_ => _
            .SetOutputDirectory(OutputDirectory / "main"));
    });

// V9 (NEW) - App-specific OutputDirectory
BuildEntry MyBuild => _ => _
    .Execute(context => {
        var contextVersion = context.Apps.First().Value;
        DotNetTasks.DotNetPack(_ => _
            .SetOutputDirectory(contextVersion.OutputDirectory / "main"));
    });
```

---

### 3. ?? **Multiple AppId Support & Mandatory AppId Requirement**

**IMPACT: HIGH** - Entry API expanded and now enforces AppId requirement

#### **MANDATORY AppId Requirement (NEW)**
**All entry types now MUST provide at least one AppId or will throw an error:**

```csharp
// V9 (WILL ERROR) - Missing AppId
BuildEntry InvalidEntry => _ => _
    .RunnerOS(RunnerOS.Ubuntu2204)
    .Execute(() => { /* ... */ });
// ERROR: AppIds for [EntryId] is empty

// V9 (REQUIRED) - Must provide AppId
BuildEntry ValidEntry => _ => _
    .AppId("my_app")
    .RunnerOS(RunnerOS.Ubuntu2204)
    .Execute(() => { /* ... */ });
```

#### **AppId Property Changes**
All entry types now support multiple AppIds instead of single AppId:

```csharp
// V8 (Still Works but now REQUIRED)
.AppId("single_app")

// V9 (New Features - all REQUIRED to have at least one)
.AppId("app1", "app2", "app3")  // Multiple apps
.AppId("single_app")            // Single app (still works)
```

#### **Interface Changes**
- ? `AppIds` property moved from `IDependentEntryDefinition` to base `IRunEntryDefinition`
- ? `DependentEntryExtensions.cs` completely removed
- ? All entry types (Build, Test, Publish) now support multiple AppIds
- ?? **All entry types now REQUIRE at least one AppId**

#### **Validation Error Messages**
```csharp
// V9 Error conditions:
throw new Exception($"AppIds for {definition.Id} is empty");
throw new Exception($"AppIds for {definition.Id} contains empty value");
```

#### **Multiple Apps OutputDirectory Access**
With multiple AppId support, each app gets its own output directory:

```csharp
// V9 - Access multiple app-specific output directories
BuildEntry MultiAppBuild => _ => _
    .AppId("app1", "app2")
    .Execute(context =>
    {
        // Each app has its own OutputDirectory
        var app1Context = context.Apps["app1"];
        var app1Output = app1Context.OutputDirectory; // .nuke/output/app1/runtime/
        
        var app2Context = context.Apps["app2"];
        var app2Output = app2Context.OutputDirectory; // .nuke/output/app2/runtime/
        
        // Process all apps
        foreach (var appContext in context.Apps.Values)
        {
            Log.Information("App: {AppId}, Output: {Output}", 
                appContext.AppId, appContext.OutputDirectory);
        }
    });
```

---

### 4. ??? **Release Assets API Completely Restructured**

**IMPACT: HIGH** - Complete removal of extension-based release asset methods

#### **Removed Extension Methods**
All `ReleaseAsset` and `ReleaseCommonAsset` extension methods completely removed:

```csharp
// V8 (REMOVED) - These no longer exist
.ReleaseAsset(OutputDirectory / "assets")
.ReleaseCommonAsset(OutputDirectory / "common")
.ReleaseAsset(context => new[] { OutputDirectory / "file.zip" })
```

#### **New Static Method API**
Replaced with static method in `BaseNukeBuildHelpers`:

```csharp
// V9 (NEW) - Use in Execute block
PublishEntry ExamplePublish => _ => _
    .AppId("my_app")
    .Execute(async context =>
    {
        var contextVersion = context.Apps.First().Value;
        
        // Add individual release assets using app-specific OutputDirectory
        await AddReleaseAsset(contextVersion.OutputDirectory / "main");
        await AddReleaseAsset(contextVersion.OutputDirectory / "archive.tar.gz");
        await AddReleaseAsset(contextVersion.OutputDirectory / "folder", "CustomName");
    });
```

#### **AddReleaseAsset Method**
```csharp
/// <summary>
/// Adds a file or directory path to the collection of individual release assets.
/// If the path is a directory, it will be zipped before being uploaded to the release.
/// </summary>
/// <param name="path">The absolute path to the file or directory to include as a release asset.</param>
/// <param name="customFilename">The custom filename of the asset for release</param>
public static async Task AddReleaseAsset(AbsolutePath path, string? customFilename = null)
```

---

### 5. ??? **Fixed Local Version Resolution**

**IMPACT: MEDIUM** - Local builds now properly resolve application versions

#### **Previous Issue (V8)**
- Local manual builds always resolved to `"0.0.0"` 
- No proper version information available during local development

#### **Fixed in V9**
- Local builds now properly resolve app versions using `ParseSemVersion` method
- Improved version resolution logic for local development scenarios
- Better integration with existing version tags and build metadata

```csharp
// V9 - Local builds now have proper version access
BuildEntry LocalBuild => _ => _
    .AppId("my_app")
    .Execute(context =>
    {
        var contextVersion = context.Apps.First().Value;
        
        if (contextVersion.IsLocal)
        {
            // Now properly resolves version instead of "0.0.0"
            var version = contextVersion.AppVersion.Version.ToString();
            Log.Information("Local build version: {version}", version);
            
            // App-specific output directory
            var output = contextVersion.OutputDirectory;
        }
    });
```

---

### 6. ??? **Entry Architecture Simplification**

**IMPACT: MEDIUM** - Removed DependentEntry concept

#### **Removed Components**
- ? `DependentEntryExtensions.cs` - Complete file removal
- ? `IDependentEntryDefinition.AppIds` property
- ? Separate dependent entry concept

#### **Architecture Changes**
- ? All entry types inherit from common `IRunEntryDefinition`
- ? Unified AppIds support across all entry types
- ? Simplified inheritance hierarchy
- ? `TargetEntryExtensions` renamed to `RunEntryExtensions`

---

## ?? **Migration Guide**

### **Step 1: Add Required AppId to All Entries**

**CRITICAL**: All entries now MUST have at least one AppId:

```csharp
// BEFORE (V8) - AppId was optional for some entries
TestEntry MyTest => _ => _
// AFTER (V9) - AppId is MANDATORY for ALL entries
TestEntry MyTest => _ => _
    .AppId("my_app")
    .Execute(() => { /* ... */ });
```

### **Step 2: Update OutputDirectory Usage**

Replace all global `OutputDirectory` references with app-specific context access:

```csharp
// BEFORE (V8) - Global OutputDirectory
.Execute(context => {
    DotNetTasks.DotNetPack(_ => _
        .SetOutputDirectory(OutputDirectory / "main"));
})

// AFTER (V9) - App-specific OutputDirectory
.Execute(context => {
    var contextVersion = context.Apps.First().Value;
    DotNetTasks.DotNetPack(_ => _
        .SetOutputDirectory(contextVersion.OutputDirectory / "main"));
})
```

### **Step 3: Update Context Usage**

Replace all context extension method calls:

```csharp
// BEFORE (V8)
.Execute(context => {
    if (context.TryGetBumpContext(out var bumpContext))
    {
        var version = bumpContext.AppVersion.Version.ToString();
        var notes = bumpContext.AppVersion.ReleaseNotes;
    }
})

// AFTER (V9)
.Execute(context => {
    var contextVersion = context.Apps.First().Value;
    if (contextVersion.BumpVersion != null)
    {
        var version = contextVersion.BumpVersion.Version.ToString();
        var notes = contextVersion.BumpVersion.ReleaseNotes;
    }
})
```

### **Step 4: Update Release Assets**

Replace extension method usage with static method calls:

```csharp
// BEFORE (V8)
PublishEntry MyPublish => _ => _
    .ReleaseAsset(OutputDirectory / "assets")
    .Execute(context => { /* ... */ });

// AFTER (V9)
PublishEntry MyPublish => _ => _
    .AppId("my_app")
    .Execute(async context => {
        var contextVersion = context.Apps.First().Value;
        await AddReleaseAsset(contextVersion.OutputDirectory / "assets");
        /* ... */
    });
```

### **Step 5: Update AppId Usage (Optional)**

Leverage new multiple AppId support if needed:

```csharp
// V9 - New multiple app support with individual OutputDirectories
BuildEntry MultiBuild => _ => _
    .AppId("app1", "app2", "app3")
    .Execute(context => {
        foreach (var appContext in context.Apps.Values)
        {
            Log.Information("Building: {AppId} to {Output}", 
                appContext.AppId, appContext.OutputDirectory);
            
            // Each app has its own output directory
            DotNetTasks.DotNetBuild(_ => _
                .SetOutputDirectory(appContext.OutputDirectory));
        }
    });
```

---

## ?? **Property Migration Table**

| V8 (Old) | V9 (New) |
|----------|----------|
| `OutputDirectory` (static) | `contextVersion.OutputDirectory` |
| AppId optional for some entries | **AppId REQUIRED for ALL entries** |
| `context.TryGetBumpContext(out var bump)` | `contextVersion.BumpVersion != null` |
| `context.TryGetPullRequestContext(out var pr)` | `contextVersion.PullRequestVersion != null` |
| `context.TryGetLocalContext(out var local)` | `contextVersion.IsLocal` |
| `context.TryGetCommitContext(out var commit)` | `contextVersion.IsCommit` |
| `bumpContext.AppVersion.Version` | `contextVersion.BumpVersion.Version` |
| `bumpContext.AppVersion.ReleaseNotes` | `contextVersion.BumpVersion.ReleaseNotes` |
| `pullRequestContext.AppVersion.Version` | `contextVersion.PullRequestVersion.Version` |
| `pullRequestContext.PullRequestNumber` | `contextVersion.PullRequestVersion.PullRequestNumber` |
| `context.RunType` | `contextVersion.RunType` |
| Single `AppId()` only | `AppId("app1", "app2", ...)` support |

---

## ?? **Complete Working Examples**

### **BuildEntry V9 Example**
```csharp
BuildEntry NukeBuildHelpersBuild => _ => _
    .AppId("nuke_build_helpers")
    .RunnerOS(RunnerOS.Ubuntu2204)
    .Execute(context => {
        var contextVersion = context.Apps.First().Value;
        
        string version = contextVersion.AppVersion.Version.ToString();
        string? releaseNotes = null;
        
        if (contextVersion.BumpVersion != null)
        {
            version = contextVersion.BumpVersion.Version.ToString();
            releaseNotes = contextVersion.BumpVersion.ReleaseNotes;
        }
        else if (contextVersion.PullRequestVersion != null)
        {
            version = contextVersion.PullRequestVersion.Version.ToString();
        }
        
        DotNetTasks.DotNetPack(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers" / "NukeBuildHelpers.csproj")
            .SetVersion(version)
            .SetPackageReleaseNotes(releaseNotes)
            .SetOutputDirectory(contextVersion.OutputDirectory / "main"));
    });
```

### **PublishEntry V9 Example**
```csharp
PublishEntry NukeBuildHelpersPublish => _ => _
    .AppId("nuke_build_helpers")
    .RunnerOS(RunnerOS.Ubuntu2204)
    .Execute(async context =>
    {
        var contextVersion = context.Apps.First().Value;
        
        if (contextVersion.IsBump)
        {
            DotNetTasks.DotNetNuGetPush(_ => _
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NuGetAuthToken)
                .SetTargetPath(contextVersion.OutputDirectory / "main" / "**"));
        }
        
        // Add release assets using app-specific OutputDirectory
        await AddReleaseAsset(contextVersion.OutputDirectory / "main");
        await AddReleaseAsset(contextVersion.OutputDirectory / "archive.tar.gz");
    });
```

### **Multiple AppId with Individual OutputDirectories Example**
```csharp
TestEntry MultiAppTest => _ => _
    .AppId("app1", "app2")
    .Execute(context =>
    {
        foreach (var appContext in context.Apps.Values)
        {
            Log.Information("Testing app: {AppId} with output: {Output}", 
                appContext.AppId, appContext.OutputDirectory);
            
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / $"{appContext.AppId}.Tests" / $"{appContext.AppId}.Tests.csproj")
                .SetResultsDirectory(appContext.OutputDirectory / "test-results"));
        }
    });

BuildEntry MultiAppBuild => _ => _
    .AppId("frontend", "backend", "shared")
    .Execute(context =>
    {
        // Build each app to its own output directory
        var frontendContext = context.Apps["frontend"];
        var backendContext = context.Apps["backend"];
        var sharedContext = context.Apps["shared"];
        
        // Each gets its own isolated output directory:
        // .nuke/output/frontend/runtime/
        // .nuke/output/backend/runtime/
        // .nuke/output/shared/runtime/
        
        DotNetTasks.DotNetBuild(_ => _
            .SetProjectFile(RootDirectory / "Frontend" / "Frontend.csproj")
            .SetOutputDirectory(frontendContext.OutputDirectory));
            
        DotNetTasks.DotNetBuild(_ => _
            .SetProjectFile(RootDirectory / "Backend" / "Backend.csproj")
            .SetOutputDirectory(backendContext.OutputDirectory));
    });
```

---

## ?? **Compilation Errors After Upgrade**

After upgrading to V9, you will encounter these compilation errors:

1. **Missing AppId Error**
   ```
   Error: AppIds for [EntryId] is empty
   ```
   **Fix**: Add `.AppId("your_app_name")` to ALL entry definitions

2. **OutputDirectory Not Found**
   ```
   Error CS0103: The name 'OutputDirectory' does not exist in the current context
   ```
   **Fix**: Replace with `contextVersion.OutputDirectory`

3. **Context Extension Methods Not Found**
   ```
   Error CS1061: 'IRunContext' does not contain a definition for 'TryGetBumpContext'
   ```
   **Fix**: Replace with `contextVersion.BumpVersion != null`

4. **Release Asset Methods Not Found**
   ```
   Error CS1061: 'IPublishEntryDefinition' does not contain a definition for 'ReleaseAsset'
   ```
   **Fix**: Use `await AddReleaseAsset(path)` in Execute block

5. **Missing Context Properties**
   ```
   Error CS0103: The name 'bumpContext' does not exist in the current context
   ```
   **Fix**: Access via `contextVersion.BumpVersion`

---

## ?? **Files Changed**

- `NukeBuildHelpers/BaseNukeBuildHelpers.cs` - **OutputDirectory property removed**
- `NukeBuildHelpers/Entry/Extensions/PublishEntryExtensions.cs` - Release asset methods removed
- `NukeBuildHelpers/Entry/Extensions/DependentEntryExtensions.cs` - **File deleted**
- `NukeBuildHelpers/RunContext/Extensions/RunContextExtensions.cs` - Extension methods removed
- `NukeBuildHelpers/Entry/Extensions/TargetEntryExtensions.cs` - Renamed to `RunEntryExtensions.cs`
- `NukeBuildHelpers/BaseNukeBuildHelpers.Tools.Common.cs` - Added `AddReleaseAsset` method
- `NukeBuildHelpers/RunContext/Models/AppRunContext.cs` - **New file with OutputDirectory property**
- `NukeBuildHelpers/Entry/Helpers/EntryHelpers.cs` - **Added AppId validation logic**
- Multiple interface files - Updated for multiple AppId support

---

## ? **Verification Checklist**

After migration, verify:

- [ ] **ALL entries have at least one AppId specified**
- [ ] All compilation errors resolved
- [ ] **All `OutputDirectory` references replaced with `contextVersion.OutputDirectory`**
- [ ] Context access updated to use `contextVersion.Apps.First().Value`
- [ ] Bump context checks use `contextVersion.BumpVersion != null`
- [ ] Pull request checks use `contextVersion.PullRequestVersion != null`
- [ ] Release assets use `await AddReleaseAsset(path)` 
- [ ] **App-specific output directories working correctly**
- [ ] Local builds properly resolve versions (not "0.0.0")
- [ ] Multiple AppId support utilized where applicable
- [ ] **Each app in multi-app setups gets isolated output directories**

---

## ?? **Need Help?**

If you encounter issues during migration:

1. Check this migration guide thoroughly
2. Review the updated documentation in `/docs/` folder
3. Examine the working examples in `build/Build.cs`
4. Open an issue on GitHub with specific error details

---

**Note**: This is a major version upgrade with significant breaking changes. Plan adequate time for migration and testing.