// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MasterMind.Console
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Nerdbank.Algorithms.NodeConstraintSelection;

    /// <summary>
    /// The main class of the console app.
    /// </summary>
    internal static class Program
    {
        private static readonly IReadOnlyDictionary<char, CodeColor> CodeColorsByFirstLetter = Enum.GetNames(typeof(CodeColor)).ToDictionary(
            n => n[0],
            n => (CodeColor)Enum.Parse(typeof(CodeColor), n));

        private static readonly string ColorChoices = string.Join(string.Empty, CodeColorsByFirstLetter.Keys);

        private static readonly Regex GuessPattern = new Regex("^[" + ColorChoices + "]{" + Rules.CodeSize + "}$", RegexOptions.IgnoreCase);

        private static void Main()
        {
            var builder = Rules.CreateSolutionBuilder();

            while (true)
            {
                ReadOnlyMemory<CodeColor> guess = InputGuess();
                Response response = InputResponse();
                builder.AddConstraint(new ResponseConstraint(guess, response));

                SolutionBuilder<CodeColor>.SolutionsAnalysis? analysis = builder.AnalyzeSolutions(CancellationToken.None);
                if (analysis.ViableSolutionsFound == 1)
                {
                    Console.WriteLine("Solution found!");
                    break;
                }

                Console.WriteLine($"{analysis.ViableSolutionsFound} solutions remaining.");
            }
        }

        private static ReadOnlyMemory<CodeColor> InputGuess()
        {
            var guess = new CodeColor[Rules.CodeSize];
            while (true)
            {
                Console.Write($"Input your guess ({ColorChoices}): ");
                string guessLine = Console.ReadLine();
                if (GuessPattern.IsMatch(guessLine))
                {
                    for (int i = 0; i < guess.Length; i++)
                    {
                        guess[i] = CodeColorsByFirstLetter[char.ToUpper(guessLine[i], CultureInfo.CurrentCulture)];
                    }

                    return guess;
                }

                Console.WriteLine("Invalid input. Specify four characters, each representing the first letter of a color.");
            }
        }

        private static Response InputResponse()
        {
            int reds;
            while (true)
            {
                Console.Write("How many red markers? ");
                if (!int.TryParse(Console.ReadLine(), out reds))
                {
                    Console.WriteLine("Invalid input. Provide an integer.");
                    continue;
                }

                break;
            }

            int whites;
            while (true)
            {
                Console.Write("How many white markers? ");
                if (!int.TryParse(Console.ReadLine(), out whites))
                {
                    Console.WriteLine("Invalid input. Provide an integer.");
                    continue;
                }

                break;
            }

            return new Response { RedCount = reds, WhiteCount = whites };
        }
    }
}
