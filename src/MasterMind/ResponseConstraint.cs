// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft;
using Nerdbank.Algorithms.NodeConstraintSelection;

namespace MasterMind;

/// <summary>
/// A constraint based on the code maker's response.
/// </summary>
public class ResponseConstraint : IConstraint<CodeColor>
{
    private readonly CodeColor[] guess;
    private readonly Response response;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseConstraint"/> class.
    /// </summary>
    /// <param name="guess">The guessed solution by the code breaker.</param>
    /// <param name="response">The code maker's feedback on the guess.</param>
    public ResponseConstraint(ReadOnlyMemory<CodeColor> guess, Response response)
    {
        Requires.Argument(guess.Length == Rules.CodeSize, nameof(guess), "Unexpected length");
        this.guess = guess.ToArray();
        this.response = response;
    }

    /// <summary>
    /// Gets the guess this constraint is based on.
    /// </summary>
    public ReadOnlyMemory<CodeColor> Guess => this.guess;

    /// <summary>
    /// Gets the code maker response this constraint requires.
    /// </summary>
    public Response Response => this.response;

    /// <inheritdoc/>
    public IReadOnlyCollection<object> Nodes => Rules.Nodes;

    /// <inheritdoc/>
    public ConstraintStates GetState(Scenario<CodeColor> scenario)
    {
        Requires.NotNull(scenario, nameof(scenario));

        Span<CodeColor> knownValues = stackalloc CodeColor[Rules.CodeSize];
        Span<bool> isKnown = stackalloc bool[Rules.CodeSize];
        int indeterminateNodeCount = CopyPartial(scenario, knownValues, isKnown);

        ConstraintStates result = ConstraintStates.None;
        if (indeterminateNodeCount == 0)
        {
            result |= ConstraintStates.Resolved;
            if (Rules.CreateResponse(this.guess, knownValues) == this.response)
            {
                result |= ConstraintStates.Satisfied;
            }

            return result;
        }

        Span<bool> forcedMask = stackalloc bool[Rules.CodeSize];
        Span<CodeColor> forcedValues = stackalloc CodeColor[Rules.CodeSize];
        EvaluateCompletions(this.guess, this.response, knownValues, isKnown, out long total, out long satisfying, out int forcedCount, forcedMask, forcedValues);
        if (satisfying == 0)
        {
            return result;
        }

        result |= ConstraintStates.Satisfiable;
        if (satisfying == total)
        {
            result |= ConstraintStates.Satisfied;
        }

        if (satisfying < total)
        {
            result |= ConstraintStates.Breakable;
        }

        if (forcedCount > 0)
        {
            result |= ConstraintStates.Resolvable;
        }

        return result;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ResponseConstraint other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => this.response.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(IConstraint<CodeColor>? other)
    {
        if (other is ResponseConstraint otherConstraint && this.response.Equals(otherConstraint.response))
        {
            for (int i = 0; i < this.guess.Length; i++)
            {
                if (this.guess[i] != otherConstraint.guess[i])
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Resolve(Scenario<CodeColor> scenario)
    {
        Requires.NotNull(scenario, nameof(scenario));

        Span<CodeColor> knownValues = stackalloc CodeColor[Rules.CodeSize];
        Span<bool> isKnown = stackalloc bool[Rules.CodeSize];
        if (CopyPartial(scenario, knownValues, isKnown) == 0)
        {
            return false;
        }

        Span<bool> forcedMask = stackalloc bool[Rules.CodeSize];
        Span<CodeColor> forcedValues = stackalloc CodeColor[Rules.CodeSize];
        EvaluateCompletions(this.guess, this.response, knownValues, isKnown, out _, out long satisfying, out int forcedCount, forcedMask, forcedValues);
        if (satisfying == 0 || forcedCount == 0)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            if (!isKnown[i] && forcedMask[i])
            {
                scenario[i] = forcedValues[i];
                changed = true;
            }
        }

        return changed;
    }

    private static int CopyPartial(Scenario<CodeColor> scenario, Span<CodeColor> knownValues, Span<bool> isKnown)
    {
        int indeterminateNodeCount = 0;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            if (scenario[i] is CodeColor value)
            {
                knownValues[i] = value;
                isKnown[i] = true;
            }
            else
            {
                isKnown[i] = false;
                indeterminateNodeCount++;
            }
        }

        return indeterminateNodeCount;
    }

    /// <summary>
    /// Evaluates every completion of a partial solution against the required response without heap allocations.
    /// </summary>
    private static void EvaluateCompletions(
        ReadOnlySpan<CodeColor> guess,
        Response required,
        Span<CodeColor> knownValues,
        Span<bool> isKnown,
        out long total,
        out long satisfying,
        out int forcedCount,
        Span<bool> forcedMask,
        Span<CodeColor> forcedValues)
    {
        Span<CodeColor> solution = stackalloc CodeColor[Rules.CodeSize];
        Span<int> freeIndices = stackalloc int[Rules.CodeSize];
        int freeCount = 0;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            if (isKnown[i])
            {
                solution[i] = knownValues[i];
            }
            else
            {
                freeIndices[freeCount++] = i;
            }
        }

        total = 0;
        satisfying = 0;
        forcedCount = 0;
        bool firstSatisfying = true;
        forcedMask.Clear();

        // Odometer over free positions (at most 6^4 completions).
        Span<int> digits = stackalloc int[Rules.CodeSize];
        digits.Clear();
        while (true)
        {
            for (int f = 0; f < freeCount; f++)
            {
                solution[freeIndices[f]] = (CodeColor)digits[f];
            }

            total++;
            if (Rules.CreateResponse(guess, solution) == required)
            {
                satisfying++;
                if (firstSatisfying)
                {
                    firstSatisfying = false;
                    for (int f = 0; f < freeCount; f++)
                    {
                        int idx = freeIndices[f];
                        forcedMask[idx] = true;
                        forcedValues[idx] = solution[idx];
                    }
                }
                else
                {
                    for (int f = 0; f < freeCount; f++)
                    {
                        int idx = freeIndices[f];
                        if (forcedMask[idx] && forcedValues[idx] != solution[idx])
                        {
                            forcedMask[idx] = false;
                        }
                    }
                }
            }

            int pos = 0;
            while (pos < freeCount)
            {
                digits[pos]++;
                if (digits[pos] < Rules.ColorCount)
                {
                    break;
                }

                digits[pos] = 0;
                pos++;
            }

            if (pos == freeCount)
            {
                break;
            }
        }

        if (satisfying > 0)
        {
            for (int i = 0; i < Rules.CodeSize; i++)
            {
                if (forcedMask[i])
                {
                    forcedCount++;
                }
            }
        }
    }
}
