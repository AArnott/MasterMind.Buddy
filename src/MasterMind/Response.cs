// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MasterMind
{
    using System;

    /// <summary>
    /// A response given by the code maker to a guess.
    /// </summary>
    public struct Response : IEquatable<Response>
    {
        /// <summary>
        /// Gets or sets the number of red markers provided by the code maker.
        /// </summary>
        public int RedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of white markers provided by the code maker.
        /// </summary>
        public int WhiteCount { get; set; }

        /// <summary>
        /// Checks equality between two <see cref="Response"/> values.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns><c>true</c> if the values are equal; <c>false</c> otherwise.</returns>
        public static bool operator ==(Response first, Response second) => first.Equals(second);

        /// <summary>
        /// Checks inequality between two <see cref="Response"/> values.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns><c>true</c> if the values are not equal; <c>false</c> otherwise.</returns>
        public static bool operator !=(Response first, Response second) => !first.Equals(second);

        /// <inheritdoc/>
        public bool Equals(Response other)
        {
            return this.RedCount == other.RedCount
                && this.WhiteCount == other.WhiteCount;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Response other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => this.RedCount + this.WhiteCount;
    }
}
