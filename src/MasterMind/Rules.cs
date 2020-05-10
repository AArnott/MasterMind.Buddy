// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MasterMind
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft;
    using Nerdbank.Algorithms.NodeConstraintSelection;

    /// <summary>
    /// Rules and functions of the game.
    /// </summary>
    public static class Rules
    {
        /// <summary>
        /// The number of positions in a code.
        /// </summary>
        public const int CodeSize = 4;

        /// <summary>
        /// The number of color options.
        /// </summary>
        public const int ColorCount = 6; // Enum.GetValues(typeof(CodeColor)).Length

        /// <summary>
        /// The nodes used to represent each position in the code.
        /// </summary>
        public static readonly IReadOnlyList<object> Nodes = Enumerable.Range(1, CodeSize).Select(n => (object)n).ToArray();

        /// <summary>
        /// Creates a new <see cref="SolutionBuilder{TNodeState}"/> to represent a game.
        /// </summary>
        /// <returns>The newly initialized instance.</returns>
        public static SolutionBuilder<CodeColor> CreateSolutionBuilder()
        {
            var possibleNodeValues = Enum.GetValues(typeof(CodeColor)).Cast<CodeColor>().ToImmutableArray();
            var builder = new SolutionBuilder<CodeColor>(Nodes, possibleNodeValues);
            return builder;
        }

        /// <summary>
        /// Adds a response to the game.
        /// </summary>
        /// <param name="builder">The builder to add the response's generated constraint to.</param>
        /// <param name="guess">The code breaker's guess.</param>
        /// <param name="response">The code maker's response.</param>
        public static void AddResponse(this SolutionBuilder<CodeColor> builder, ReadOnlyMemory<CodeColor> guess, Response response)
        {
            Requires.NotNull(builder, nameof(builder));
            builder.AddConstraint(new ResponseConstraint(guess, response));
        }

        /// <summary>
        /// Constructs a valid code maker response to a code breaker guess.
        /// </summary>
        /// <param name="guess">The code breaker's guess.</param>
        /// <param name="solution">The solution used to create a response.</param>
        /// <returns>Feedback on the code breaker's guess.</returns>
        public static Response CreateResponse(ReadOnlySpan<CodeColor> guess, ReadOnlySpan<CodeColor> solution)
        {
            Requires.Argument(guess.Length == solution.Length, null, "Guess and solution must have the same length.");

            Response result = default;

            // Count red nodes.
            Span<bool> match = stackalloc bool[CodeSize];
            for (int i = 0; i < CodeSize; i++)
            {
                if (guess[i] == solution[i])
                {
                    match[i] = true;
                    result.RedCount++;
                }
            }

            // Count how many times each color appears in the solution that was not an exact match.
            Span<int> remainingColorsInSolution = stackalloc int[ColorCount];
            for (int i = 0; i < CodeSize; i++)
            {
                if (!match[i])
                {
                    if (solution[i] is { } color)
                    {
                        remainingColorsInSolution[(int)color]++;
                    }
                }
            }

            // For each occurrence of a solution color in the guess that was not an exact match, award one white marker.
            for (int i = 0; i < CodeSize; i++)
            {
                if (!match[i])
                {
                    if (remainingColorsInSolution[(int)guess[i]] > 0)
                    {
                        remainingColorsInSolution[(int)guess[i]]--;
                        result.WhiteCount++;
                    }
                }
            }

            return result;
        }
    }
}
