// <copyright file="StringExtensions.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;

    /// <summary>
    /// Extension methods for <see cref="string"/>.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Checks if a string contains another string with string comparison option.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="value">The string to be found.</param>
        /// <param name="comp">String comparison option.</param>
        /// <returns>Returns true if the string was found, other wise false is returned.</returns>
        public static bool Contains(this string source, string value, StringComparison comp)
        {
            return source?.IndexOf(value, comp) >= 0;
        }
    }
}
