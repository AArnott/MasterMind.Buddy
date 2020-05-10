// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MasterMind
{
    using System;
    using System.Collections.Generic;
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

            Response response = Rules.CreateResponse(this.guess.Span, default);
            ConstraintStates result = ConstraintStates.None;

            if (this.response.Equals(response))
            {
                result |= ConstraintStates.Satisfied;
            }

            bool anyUnresolved = false;
            for (int i = 0; i < scenario.NodeCount; i++)
            {
                anyUnresolved |= !scenario[i].HasValue;
            }

            if (anyUnresolved)
            {
                result |= ConstraintStates.Satisfiable;
            }
            else
            {
                result |= ConstraintStates.Resolved;
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
        public bool Resolve(Scenario<CodeColor> scenario) => false;
    }
}
