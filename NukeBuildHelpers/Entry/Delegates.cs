using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry;

/// <summary>
/// Delegate for defining a test entry.
/// </summary>
/// <param name="definition">The test entry definition.</param>
/// <returns>The test entry definition after transformation.</returns>
public delegate ITestEntryDefinition TestEntry(ITestEntryDefinition definition);

/// <summary>
/// Delegate for defining a build entry.
/// </summary>
/// <param name="definition">The build entry definition.</param>
/// <returns>The build entry definition after transformation.</returns>
public delegate IBuildEntryDefinition BuildEntry(IBuildEntryDefinition definition);

/// <summary>
/// Delegate for defining a publish entry.
/// </summary>
/// <param name="definition">The publish entry definition.</param>
/// <returns>The publish entry definition after transformation.</returns>
public delegate IPublishEntryDefinition PublishEntry(IPublishEntryDefinition definition);
