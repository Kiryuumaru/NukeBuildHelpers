﻿using NukeBuildHelpers.Entry.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IEntryDefinition"/> to configure various aspects of the matrix entry.
/// </summary>
public static class MatrixEntryExtensions
{
    /// <summary>
    /// Sets the matrix of the definition to configure on each matrix element.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of target entry definition.</typeparam>
    /// <typeparam name="TMatrix">The type of the matrix.</typeparam>
    /// <param name="definition">The target entry definition instance.</param>
    /// <param name="matrix">The value of the matrix array.</param>
    /// <param name="matrixDefinition">The matrix definition to configure on each matrix element.</param>
    /// <returns>The modified target entry definition instance.</returns>
    public static TEntryDefinition Matrix<TEntryDefinition, TMatrix>(this TEntryDefinition definition, TMatrix[] matrix, Action<TEntryDefinition, TMatrix> matrixDefinition)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Matrix.Add(clonedDefinition => Task.Run(() =>
        {
            List<IEntryDefinition> definitions = [];
            foreach (var mat in matrix)
            {
                var subClonedDefinition = (TEntryDefinition)clonedDefinition.Clone();
                matrixDefinition(subClonedDefinition, mat);
                definitions.Add(subClonedDefinition);
            }
            return definitions.ToArray();
        }));
        return definition;
    }
}