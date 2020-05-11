﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MasterMind
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft;
    using Nerdbank.Algorithms.NodeConstraintSelection;

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

        /// <inheritdoc/>
        public IReadOnlyCollection<object> Nodes => Rules.Nodes;

        /// <inheritdoc/>
        public ConstraintStates GetState(Scenario<CodeColor> scenario)
        {
            Requires.NotNull(scenario, nameof(scenario));

            // First determine state of Resolved flag.
            int indeterminateNodeCount = 0;
            int exactMatches = 0;
            for (int i = 0; i < this.Nodes.Count; i++)
            {
                if (scenario[i] is null)
                {
                    indeterminateNodeCount++;
                }

                if (scenario[i] == this.guess.Span[i])
                {
                    exactMatches++;
                }
            }

            var result = ConstraintStates.None;

            if (indeterminateNodeCount == 0)
            {
                result |= ConstraintStates.Resolved;
            }

            // If the guess was completely wrong, then we know that no color used in the guess
            // can possibly be in the solution.
            if (this.response.RedCount == 0 && this.response.WhiteCount == 0)
            {
                for (int i = 0; i < Rules.CodeSize; i++)
                {
                    for (int j = 0; j < Rules.CodeSize; j++)
                    {
                        if (scenario[i] == this.guess.Span[j])
                        {
                            // Unsatisfiable result.
                            return result;
                        }
                    }
                }
            }

            // If the number of exact matches exceeds the red count, we're already broken.
            if (exactMatches > this.response.RedCount)
            {
                return result;
            }

            // So long as *any* nodes are undetermined they can be made to not match the guess.
            if (indeterminateNodeCount > 0)
            {
                result |= ConstraintStates.Breakable;
            }

            if (exactMatches + indeterminateNodeCount >= this.response.RedCount)
            {
                result |= ConstraintStates.Satisfiable;

                if (indeterminateNodeCount > 0 && exactMatches + indeterminateNodeCount == this.response.RedCount)
                {
                    result |= ConstraintStates.Resolvable;
                }
            }

            if (indeterminateNodeCount == 0 && (result & ConstraintStates.Satisfiable) == ConstraintStates.Satisfiable)
            {
                result |= ConstraintStates.Satisfied;
            }

            return result;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ResponseConstraint other && this.Equals(other);

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
            if ((this.GetState(scenario) & ConstraintStates.Resolvable) == ConstraintStates.Resolvable)
            {
                for (int i = 0; i < this.guess.Length; i++)
                {
                    if (scenario[i] is null)
                    {
                        scenario[i] = this.guess.Span[i];
                    }
                }

                return true;
            }

            return false;
        }
    }
}
