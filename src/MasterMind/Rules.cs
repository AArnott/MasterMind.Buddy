// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft;
using Nerdbank.Algorithms.NodeConstraintSelection;

namespace MasterMind;

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
    /// The total number of distinct codes.
    /// </summary>
    public const int CodeSpaceSize = ColorCount * ColorCount * ColorCount * ColorCount;

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
        ImmutableArray<CodeColor> possibleNodeValues = Enum.GetValues(typeof(CodeColor)).Cast<CodeColor>().ToImmutableArray();
        SolutionBuilder<CodeColor> builder = new(Nodes, possibleNodeValues);
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
                CodeColor color = solution[i];
                remainingColorsInSolution[(int)color]++;
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

    /// <summary>
    /// Enumerates every code in the game's code space.
    /// </summary>
    /// <returns>All possible codes.</returns>
    public static IEnumerable<CodeColor[]> EnumerateCodeSpace()
    {
        CodeColor[] code = new CodeColor[CodeSize];
        foreach (CodeColor[] item in EnumerateCodeSpace(code, 0))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Finds every code still consistent with the constraints recorded on a solution builder.
    /// </summary>
    /// <param name="builder">The builder containing response constraints.</param>
    /// <returns>The remaining viable solutions.</returns>
    public static List<CodeColor[]> GetRemainingSolutions(SolutionBuilder<CodeColor> builder)
    {
        Requires.NotNull(builder, nameof(builder));

        List<ResponseConstraint> constraints = builder.Constraints.OfType<ResponseConstraint>().ToList();
        List<CodeColor[]> remaining = new();
        foreach (CodeColor[] code in EnumerateCodeSpace())
        {
            if (IsConsistentWith(code, constraints))
            {
                remaining.Add(code);
            }
        }

        return remaining;
    }

    /// <summary>
    /// Suggests a next guess that minimizes the worst-case number of remaining solutions.
    /// </summary>
    /// <param name="builder">The builder containing response constraints so far.</param>
    /// <returns>
    /// A recommended guess, or <see langword="null"/> if no viable solutions remain.
    /// </returns>
    /// <remarks>
    /// Uses Knuth-style minimax over the full code space as candidate guesses, scoring each by the
    /// size of its largest response partition among the remaining solutions. Ties prefer guesses
    /// that are themselves still viable solutions.
    /// </remarks>
    public static CodeColor[]? SuggestGuess(SolutionBuilder<CodeColor> builder)
    {
        Requires.NotNull(builder, nameof(builder));

        List<CodeColor[]> remaining = GetRemainingSolutions(builder);
        if (remaining.Count == 0)
        {
            return null;
        }

        if (remaining.Count == 1)
        {
            return remaining[0];
        }

        // Hash viable solutions for O(1) tie-breaking.
        HashSet<string> remainingKeys = new(remaining.Select(CodeKey));

        CodeColor[]? bestGuess = null;
        int bestWorstCase = int.MaxValue;
        bool bestIsViable = false;
        long bestExpectedNumer = long.MaxValue; // expected remaining * remaining.Count, lower is better

        foreach (CodeColor[] guess in EnumerateCodeSpace())
        {
            // Partition remaining solutions by the response this guess would produce.
            Dictionary<Response, int> partitions = new();
            int worstCase = 0;
            foreach (CodeColor[] solution in remaining)
            {
                Response response = CreateResponse(guess, solution);
                partitions.TryGetValue(response, out int count);
                count++;
                partitions[response] = count;
                if (count > worstCase)
                {
                    worstCase = count;
                }
            }

            long expectedNumer = 0;
            foreach (int count in partitions.Values)
            {
                expectedNumer += (long)count * count;
            }

            bool isViable = remainingKeys.Contains(CodeKey(guess));

            bool better =
                worstCase < bestWorstCase ||
                (worstCase == bestWorstCase && expectedNumer < bestExpectedNumer) ||
                (worstCase == bestWorstCase && expectedNumer == bestExpectedNumer && isViable && !bestIsViable);

            if (better)
            {
                bestWorstCase = worstCase;
                bestExpectedNumer = expectedNumer;
                bestIsViable = isViable;
                bestGuess = (CodeColor[])guess.Clone();
            }
        }

        return bestGuess;
    }

    private static bool IsConsistentWith(ReadOnlySpan<CodeColor> code, List<ResponseConstraint> constraints)
    {
        foreach (ResponseConstraint constraint in constraints)
        {
            if (CreateResponse(constraint.Guess.Span, code) != constraint.Response)
            {
                return false;
            }
        }

        return true;
    }

    private static string CodeKey(CodeColor[] code) => string.Concat(code.Select(c => (char)('0' + (int)c)));

    private static IEnumerable<CodeColor[]> EnumerateCodeSpace(CodeColor[] code, int index)
    {
        if (index == CodeSize)
        {
            yield return (CodeColor[])code.Clone();
            yield break;
        }

        for (int color = 0; color < ColorCount; color++)
        {
            code[index] = (CodeColor)color;
            foreach (CodeColor[] item in EnumerateCodeSpace(code, index + 1))
            {
                yield return item;
            }
        }
    }
}
