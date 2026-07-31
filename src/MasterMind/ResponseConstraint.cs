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
    private readonly ReadOnlyMemory<CodeColor> guess;
    private readonly Response response;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseConstraint"/> class.
    /// </summary>
    /// <param name="guess">The guessed solution by the code breaker.</param>
    /// <param name="response">The code maker's feedback on the guess.</param>
    public ResponseConstraint(ReadOnlyMemory<CodeColor> guess, Response response)
    {
        Requires.Argument(guess.Length == Rules.CodeSize, nameof(guess), "Unexpected length");
        this.guess = guess;
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

        CodeColor?[] partial = new CodeColor?[Rules.CodeSize];
        int indeterminateNodeCount = 0;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            partial[i] = scenario[i];
            if (partial[i] is null)
            {
                indeterminateNodeCount++;
            }
        }

        ConstraintStates result = ConstraintStates.None;
        if (indeterminateNodeCount == 0)
        {
            result |= ConstraintStates.Resolved;
        }

        CompletionStats stats = EvaluateCompletions(this.guess.Span, this.response, partial);
        if (stats.SatisfyingCompletions == 0)
        {
            return result;
        }

        result |= ConstraintStates.Satisfiable;

        if (stats.SatisfyingCompletions == stats.TotalCompletions)
        {
            result |= ConstraintStates.Satisfied;
        }

        if (indeterminateNodeCount > 0)
        {
            if (stats.SatisfyingCompletions < stats.TotalCompletions)
            {
                result |= ConstraintStates.Breakable;
            }

            if (stats.ForcedAssignmentCount > 0)
            {
                result |= ConstraintStates.Resolvable;
            }
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
                if (this.guess.Span[i] != otherConstraint.guess.Span[i])
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

        CodeColor?[] partial = new CodeColor?[Rules.CodeSize];
        bool anyIndeterminate = false;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            partial[i] = scenario[i];
            anyIndeterminate |= partial[i] is null;
        }

        if (!anyIndeterminate)
        {
            return false;
        }

        CompletionStats stats = EvaluateCompletions(this.guess.Span, this.response, partial);
        if (stats.ForcedAssignmentCount == 0)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            if (scenario[i] is null && stats.ForcedMask[i])
            {
                scenario[i] = stats.ForcedValues[i];
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Evaluates every completion of a partial solution against the required response.
    /// </summary>
    private static CompletionStats EvaluateCompletions(ReadOnlySpan<CodeColor> guess, Response required, CodeColor?[] partial)
    {
        // Copy guess so the recursive walk can use a stable array without capturing a span.
        CodeColor[] guessArray = guess.ToArray();
        CodeColor[] solution = new CodeColor[Rules.CodeSize];
        bool[] forcedMask = new bool[Rules.CodeSize];
        CodeColor[] forcedValues = new CodeColor[Rules.CodeSize];

        long total = 0;
        long satisfying = 0;
        bool firstSatisfying = true;

        void Recurse(int index)
        {
            if (index == Rules.CodeSize)
            {
                total++;
                if (Rules.CreateResponse(guessArray, solution) == required)
                {
                    satisfying++;
                    if (firstSatisfying)
                    {
                        firstSatisfying = false;
                        for (int i = 0; i < Rules.CodeSize; i++)
                        {
                            if (partial[i] is null)
                            {
                                forcedMask[i] = true;
                                forcedValues[i] = solution[i];
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < Rules.CodeSize; i++)
                        {
                            if (forcedMask[i] && forcedValues[i] != solution[i])
                            {
                                forcedMask[i] = false;
                            }
                        }
                    }
                }

                return;
            }

            if (partial[index] is CodeColor known)
            {
                solution[index] = known;
                Recurse(index + 1);
                return;
            }

            for (int color = 0; color < Rules.ColorCount; color++)
            {
                solution[index] = (CodeColor)color;
                Recurse(index + 1);
            }
        }

        Recurse(0);

        int forcedCount = 0;
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            if (forcedMask[i])
            {
                forcedCount++;
            }
        }

        return new CompletionStats(total, satisfying, forcedCount, forcedValues, forcedMask);
    }

    private readonly struct CompletionStats
    {
        internal CompletionStats(long totalCompletions, long satisfyingCompletions, int forcedAssignmentCount, CodeColor[] forcedValues, bool[] forcedMask)
        {
            this.TotalCompletions = totalCompletions;
            this.SatisfyingCompletions = satisfyingCompletions;
            this.ForcedAssignmentCount = forcedAssignmentCount;
            this.ForcedValues = forcedValues;
            this.ForcedMask = forcedMask;
        }

        internal long TotalCompletions { get; }

        internal long SatisfyingCompletions { get; }

        internal int ForcedAssignmentCount { get; }

        internal CodeColor[] ForcedValues { get; }

        internal bool[] ForcedMask { get; }
    }
}
