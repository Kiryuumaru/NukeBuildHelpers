using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IRunEntryDefinition"/> to configure various aspects of the matrix entry.
/// </summary>
public static class MatrixEntryExtensions
{
    /// <summary>
    /// Sets the matrix of the definition to configure on each matrix element.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of target entry definition.</typeparam>
    /// <typeparam name="TMatrix">The type of the matrix.</typeparam>
    /// <param name="definition">The target entry definition instance.</param>
    /// <param name="matrix">The value of the matrix array.</param>
    /// <param name="matrixDefinition">The matrix definition to configure on each matrix element.</param>
    /// <returns>The modified target entry definition instance.</returns>
    public static TRunEntryDefinition Matrix<TRunEntryDefinition, TMatrix>(this TRunEntryDefinition definition, TMatrix[] matrix, Action<TRunEntryDefinition, TMatrix> matrixDefinition)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Matrix;
        value.Add(clonedDefinition => Task.Run(() =>
        {
            List<IRunEntryDefinition> definitions = [];
            foreach (var mat in matrix)
            {
                var subClonedDefinition = (TRunEntryDefinition)clonedDefinition.Clone();
                matrixDefinition(subClonedDefinition, mat);
                definitions.Add(subClonedDefinition);
            }
            return definitions.ToArray();
        }));
        definition.Matrix = value;
        return definition;
    }
}
