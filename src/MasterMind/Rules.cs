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
    /// Maximum distinct response keys used when partitioning guesses (red 0-4, white 0-4).
    /// </summary>
    private const int ResponseKeyCount = 5 * 5;

    /// <summary>
    /// Flat color table for every packed code. Index = (packed * CodeSize) + position.
    /// </summary>
    private static readonly CodeColor[] CodeSpaceColors = CreateCodeSpaceColors();

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

        Span<bool> match = stackalloc bool[CodeSize];
        for (int i = 0; i < CodeSize; i++)
        {
            if (guess[i] == solution[i])
            {
                match[i] = true;
                result.RedCount++;
            }
        }

        Span<int> remainingColorsInSolution = stackalloc int[ColorCount];
        for (int i = 0; i < CodeSize; i++)
        {
            if (!match[i])
            {
                remainingColorsInSolution[(int)solution[i]]++;
            }
        }

        for (int i = 0; i < CodeSize; i++)
        {
            if (!match[i] && remainingColorsInSolution[(int)guess[i]] > 0)
            {
                remainingColorsInSolution[(int)guess[i]]--;
                result.WhiteCount++;
            }
        }

        return result;
    }

    /// <summary>
    /// Packs a code into a single integer in base <see cref="ColorCount"/>.
    /// </summary>
    /// <param name="code">The code to pack.</param>
    /// <returns>The packed representation.</returns>
    public static int PackCode(ReadOnlySpan<CodeColor> code)
    {
        Requires.Argument(code.Length == CodeSize, nameof(code), "Unexpected length");
        return (int)code[0]
            + (ColorCount * (int)code[1])
            + (ColorCount * ColorCount * (int)code[2])
            + (ColorCount * ColorCount * ColorCount * (int)code[3]);
    }

    /// <summary>
    /// Unpacks a code previously produced by <see cref="PackCode"/>.
    /// </summary>
    /// <param name="packed">The packed code.</param>
    /// <param name="destination">A span of length <see cref="CodeSize"/> to receive the colors.</param>
    public static void UnpackCode(int packed, Span<CodeColor> destination)
    {
        Requires.Argument(destination.Length >= CodeSize, nameof(destination), "Unexpected length");
        destination[0] = (CodeColor)(packed % ColorCount);
        packed /= ColorCount;
        destination[1] = (CodeColor)(packed % ColorCount);
        packed /= ColorCount;
        destination[2] = (CodeColor)(packed % ColorCount);
        packed /= ColorCount;
        destination[3] = (CodeColor)packed;
    }

    /// <summary>
    /// Enumerates every code in the game's code space.
    /// </summary>
    /// <returns>All possible codes.</returns>
    public static IEnumerable<CodeColor[]> EnumerateCodeSpace()
    {
        for (int i = 0; i < CodeSpaceSize; i++)
        {
            yield return ToCodeArray(i);
        }
    }

    /// <summary>
    /// Finds every code still consistent with the constraints recorded on a solution builder.
    /// </summary>
    /// <param name="builder">The builder containing response constraints.</param>
    /// <returns>The remaining viable solutions.</returns>
    public static List<CodeColor[]> GetRemainingSolutions(SolutionBuilder<CodeColor> builder)
    {
        List<int> packed = GetRemainingPackedSolutions(builder);
        List<CodeColor[]> remaining = new(packed.Count);
        for (int i = 0; i < packed.Count; i++)
        {
            remaining.Add(ToCodeArray(packed[i]));
        }

        return remaining;
    }

    /// <summary>
    /// Returns packed codes still consistent with the builder's response constraints.
    /// </summary>
    /// <param name="builder">The builder containing response constraints.</param>
    /// <returns>Packed remaining solutions.</returns>
    public static List<int> GetRemainingPackedSolutions(SolutionBuilder<CodeColor> builder)
    {
        Requires.NotNull(builder, nameof(builder));

        int constraintCount = 0;
        foreach (IConstraint<CodeColor> constraint in builder.Constraints)
        {
            if (constraint is ResponseConstraint)
            {
                constraintCount++;
            }
        }

        ReadOnlyMemory<CodeColor>[] guesses = new ReadOnlyMemory<CodeColor>[constraintCount];
        Response[] responses = new Response[constraintCount];
        int index = 0;
        foreach (IConstraint<CodeColor> constraint in builder.Constraints)
        {
            if (constraint is ResponseConstraint responseConstraint)
            {
                guesses[index] = responseConstraint.Guess;
                responses[index] = responseConstraint.Response;
                index++;
            }
        }

        // Flat cache of every code's colors: index = (packed * CodeSize) + position.
        CodeColor[] allColors = CodeSpaceColors;
        List<int> remaining = new();

        for (int packed = 0; packed < CodeSpaceSize; packed++)
        {
            ReadOnlySpan<CodeColor> codeColors = allColors.AsSpan(packed * CodeSize, CodeSize);
            bool ok = true;
            for (int c = 0; c < constraintCount; c++)
            {
                if (CreateResponse(guesses[c].Span, codeColors) != responses[c])
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                remaining.Add(packed);
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
        List<int> remaining = GetRemainingPackedSolutions(builder);
        int? packed = SuggestGuess(remaining);
        return packed is int value ? ToCodeArray(value) : null;
    }

    /// <summary>
    /// Suggests a next guess from an already-computed remaining solution set.
    /// </summary>
    /// <param name="remainingPacked">Remaining solutions as packed codes.</param>
    /// <returns>The packed recommended guess, or <see langword="null"/> if none remain.</returns>
    public static int? SuggestGuess(IReadOnlyList<int> remainingPacked)
    {
        Requires.NotNull(remainingPacked, nameof(remainingPacked));

        if (remainingPacked.Count == 0)
        {
            return null;
        }

        if (remainingPacked.Count == 1)
        {
            return remainingPacked[0];
        }

        Span<bool> viable = stackalloc bool[CodeSpaceSize];
        viable.Clear();
        for (int i = 0; i < remainingPacked.Count; i++)
        {
            viable[remainingPacked[i]] = true;
        }

        // Flatten remaining solutions once so the inner loop avoids repeated unpacking.
        CodeColor[] remainingColors = new CodeColor[remainingPacked.Count * CodeSize];
        CodeColor[] allColors = CodeSpaceColors;
        for (int i = 0; i < remainingPacked.Count; i++)
        {
            allColors.AsSpan(remainingPacked[i] * CodeSize, CodeSize).CopyTo(remainingColors.AsSpan(i * CodeSize, CodeSize));
        }

        Span<int> partitions = stackalloc int[ResponseKeyCount];

        int bestGuess = -1;
        int bestWorstCase = int.MaxValue;
        bool bestIsViable = false;
        long bestExpectedNumer = long.MaxValue;

        for (int guess = 0; guess < CodeSpaceSize; guess++)
        {
            ReadOnlySpan<CodeColor> guessColors = allColors.AsSpan(guess * CodeSize, CodeSize);
            partitions.Clear();

            int worstCase = 0;
            bool doomed = false;

            for (int s = 0; s < remainingPacked.Count; s++)
            {
                ReadOnlySpan<CodeColor> solutionColors = remainingColors.AsSpan(s * CodeSize, CodeSize);
                Response response = CreateResponse(guessColors, solutionColors);
                int count = ++partitions[ResponseKey(response)];
                if (count > worstCase)
                {
                    worstCase = count;
                    if (worstCase > bestWorstCase)
                    {
                        doomed = true;
                        break;
                    }
                }
            }

            if (doomed)
            {
                continue;
            }

            long expectedNumer = 0;
            for (int i = 0; i < partitions.Length; i++)
            {
                int count = partitions[i];
                if (count != 0)
                {
                    expectedNumer += (long)count * count;
                }
            }

            bool isViable = viable[guess];
            bool better =
                worstCase < bestWorstCase ||
                (worstCase == bestWorstCase && expectedNumer < bestExpectedNumer) ||
                (worstCase == bestWorstCase && expectedNumer == bestExpectedNumer && isViable && !bestIsViable);

            if (better)
            {
                bestWorstCase = worstCase;
                bestExpectedNumer = expectedNumer;
                bestIsViable = isViable;
                bestGuess = guess;
            }
        }

        return bestGuess >= 0 ? bestGuess : null;
    }

    /// <summary>
    /// Fills a [<see cref="CodeSize"/> * <see cref="ColorCount"/>] histogram of position/color
    /// frequencies for the given packed solutions. Index is <c>(position * ColorCount) + color</c>.
    /// </summary>
    /// <param name="remainingPacked">Remaining packed solutions.</param>
    /// <param name="nodeValueCounts">Destination histogram storage.</param>
    public static void CountNodeValues(IReadOnlyList<int> remainingPacked, Span<int> nodeValueCounts)
    {
        Requires.NotNull(remainingPacked, nameof(remainingPacked));
        Requires.Argument(nodeValueCounts.Length >= CodeSize * ColorCount, nameof(nodeValueCounts), "Unexpected length");
        nodeValueCounts.Slice(0, CodeSize * ColorCount).Clear();

        CodeColor[] allColors = CodeSpaceColors;
        for (int i = 0; i < remainingPacked.Count; i++)
        {
            ReadOnlySpan<CodeColor> colors = allColors.AsSpan(remainingPacked[i] * CodeSize, CodeSize);
            for (int pos = 0; pos < CodeSize; pos++)
            {
                nodeValueCounts[(pos * ColorCount) + (int)colors[pos]]++;
            }
        }
    }

    private static int ResponseKey(Response response) => response.RedCount + (5 * response.WhiteCount);

    private static CodeColor[] CreateCodeSpaceColors()
    {
        CodeColor[] colors = new CodeColor[CodeSpaceSize * CodeSize];
        for (int packed = 0; packed < CodeSpaceSize; packed++)
        {
            int value = packed;
            int baseIndex = packed * CodeSize;
            colors[baseIndex] = (CodeColor)(value % ColorCount);
            value /= ColorCount;
            colors[baseIndex + 1] = (CodeColor)(value % ColorCount);
            value /= ColorCount;
            colors[baseIndex + 2] = (CodeColor)(value % ColorCount);
            value /= ColorCount;
            colors[baseIndex + 3] = (CodeColor)value;
        }

        return colors;
    }

    private static CodeColor[] ToCodeArray(int packed)
    {
        CodeColor[] code = new CodeColor[CodeSize];
        CodeSpaceColors.AsSpan(packed * CodeSize, CodeSize).CopyTo(code);
        return code;
    }
}
