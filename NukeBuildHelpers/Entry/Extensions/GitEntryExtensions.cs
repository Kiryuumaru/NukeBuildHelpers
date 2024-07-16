using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IRunEntryDefinition"/> to configure various aspects of the git entry.
/// </summary>
public static class GitEntryExtensions
{
    /// <summary>
    /// Sets the number of commits to fetch. 0 indicates all history for all branches and tags.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchDepth">The fetch depth to set.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchDepth<TRunEntryDefinition>(this TRunEntryDefinition definition, int checkoutFetchDepth)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchDepth = _ => Task.Run(() => checkoutFetchDepth);
        return definition;
    }

    /// <summary>
    /// Sets the number of commits to fetch using a function that returns the value. 0 indicates all history for all branches and tags.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchDepth">The function that returns the fetch depth.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchDepth<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<int> checkoutFetchDepth)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchDepth = _ => Task.Run(() => checkoutFetchDepth());
        return definition;
    }

    /// <summary>
    /// Sets the number of commits to fetch using a function that takes a run context and returns the value. 0 indicates all history for all branches and tags.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchDepth">The function that takes a run context and returns the fetch depth.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchDepth<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, int> checkoutFetchDepth)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchDepth = runContext => Task.Run(() => checkoutFetchDepth(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the number of commits to fetch using an asynchronous function that returns the value. 0 indicates all history for all branches and tags.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchDepth">The asynchronous function that returns the fetch depth.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchDepth<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<int>> checkoutFetchDepth)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchDepth = _ => Task.Run(async () => await checkoutFetchDepth());
        return definition;
    }

    /// <summary>
    /// Sets the number of commits to fetch using an asynchronous function that takes a run context and returns the value. 0 indicates all history for all branches and tags.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchDepth">The asynchronous function that takes a run context and returns the fetch depth.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchDepth<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<int>> checkoutFetchDepth)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchDepth = runContext => Task.Run(async () => await checkoutFetchDepth(runContext));
        return definition;
    }

    /// <summary>
    /// Sets <c>true</c> whether to fetch tags, even if fetch-depth > 0, otherwise <c>false</c>.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchTags">The fetch tags to set.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchTags<TRunEntryDefinition>(this TRunEntryDefinition definition, bool checkoutFetchTags)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchTags = _ => Task.Run(() => checkoutFetchTags);
        return definition;
    }

    /// <summary>
    /// Sets <c>true</c> whether to fetch tags using a function that returns the value, even if fetch-depth > 0, otherwise <c>false</c>.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchTags">The function that returns the fetch tags.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchTags<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<bool> checkoutFetchTags)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchTags = _ => Task.Run(() => checkoutFetchTags());
        return definition;
    }

    /// <summary>
    /// Sets <c>true</c> whether to fetch tags using a function that takes a run context and returns the value, even if fetch-depth > 0, otherwise <c>false</c>.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchTags">The function that takes a run context and returns the fetch tags.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchTags<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, bool> checkoutFetchTags)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchTags = runContext => Task.Run(() => checkoutFetchTags(runContext));
        return definition;
    }

    /// <summary>
    /// Sets <c>true</c> whether to fetch tags using an asynchronous function that returns the value, even if fetch-depth > 0, otherwise <c>false</c>.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchTags">The asynchronous function that returns the fetch tags.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchTags<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<bool>> checkoutFetchTags)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchTags = _ => Task.Run(async () => await checkoutFetchTags());
        return definition;
    }

    /// <summary>
    /// Sets <c>true</c> whether to fetch tags using an asynchronous function that takes a run context and returns the value, even if fetch-depth > 0, otherwise <c>false</c>.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutFetchTags">The asynchronous function that takes a run context and returns the fetch tags.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutFetchTags<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<bool>> checkoutFetchTags)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutFetchTags = runContext => Task.Run(async () => await checkoutFetchTags(runContext));
        return definition;
    }

    /// <summary>
    /// Sets value on how to checkout submodules.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutSubmodules">The type of how to checkout submodules.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutSubmodules<TRunEntryDefinition>(this TRunEntryDefinition definition, SubmoduleCheckoutType checkoutSubmodules)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutSubmodules = _ => Task.Run(() => checkoutSubmodules);
        return definition;
    }

    /// <summary>
    /// Sets value on how to checkout submodules using a function that returns the value.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutSubmodules">The function that returns the type of how to checkout submodules.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutSubmodules<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<SubmoduleCheckoutType> checkoutSubmodules)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutSubmodules = _ => Task.Run(() => checkoutSubmodules());
        return definition;
    }

    /// <summary>
    /// Sets value on how to checkout submodules using a function that takes a run context and returns the value.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutSubmodules">The function that takes a run context and returns the type of how to checkout submodules.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutSubmodules<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, SubmoduleCheckoutType> checkoutSubmodules)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutSubmodules = runContext => Task.Run(() => checkoutSubmodules(runContext));
        return definition;
    }

    /// <summary>
    /// Sets value on how to checkout submodules using an asynchronous function that returns the value.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutSubmodules">The asynchronous function that returns the type of how to checkout submodules.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutSubmodules<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<SubmoduleCheckoutType>> checkoutSubmodules)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutSubmodules = _ => Task.Run(async () => await checkoutSubmodules());
        return definition;
    }

    /// <summary>
    /// Sets value on how to checkout submodules using an asynchronous function that takes a run context and returns the value.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="checkoutSubmodules">The asynchronous function that takes a run context and returns the type of how to checkout submodules.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CheckoutSubmodules<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<SubmoduleCheckoutType>> checkoutSubmodules)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CheckoutSubmodules = runContext => Task.Run(async () => await checkoutSubmodules(runContext));
        return definition;
    }
}
