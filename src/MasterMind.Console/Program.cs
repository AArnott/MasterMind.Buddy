// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MasterMind.Console
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
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
            Console.Write("What role are you playing (M = Code _Maker, B = Code _Breaker)? ");
            while (true)
            {
                switch (char.ToUpper(Console.ReadKey().KeyChar, CultureInfo.CurrentCulture))
                {
                    case 'M':
                        Console.WriteLine("Code maker");
                        CodeMaker();
                        return;
                    case 'B':
                        Console.WriteLine("Code breaker");
                        CodeBreaker();
                        return;
                }
            }
        }

        private static void CodeMaker()
        {
            CodeColor[] code = new CodeColor[Rules.CodeSize];
            while (true)
            {
                Console.Write("Enter code (or leave blank to generate one): ");
                string codeLine = Console.ReadLine();
                if (codeLine.Length == 0)
                {
                    var random = new Random();
                    for (int i = 0; i < code.Length; i++)
                    {
                        code[i] = (CodeColor)random.Next(Rules.ColorCount);
                    }

                    break;
                }
                else if (TryParseCode(codeLine, code))
                {
                    break;
                }
            }

            Console.WriteLine("Your code is: {0}", string.Join(", ", code));

            int breakerAttemptsCount = 0;
            while (true)
            {
                ReadOnlySpan<CodeColor> guess = InputCode("What is the code breaker's guess?").Span;
                var response = Rules.CreateResponse(guess, code);
                Console.WriteLine($"{response.RedCount} red pins, {response.WhiteCount} white pins.");
                if (response.RedCount == Rules.CodeSize)
                {
                    Console.WriteLine("Game over. Code breaker wins.");
                    break;
                }

                if (++breakerAttemptsCount == 10)
                {
                    Console.WriteLine("Game over. YOU win.");
                    break;
                }
            }
        }

        private static void CodeBreaker()
        {
            var builder = Rules.CreateSolutionBuilder();

            int breakerAttemptsCount = 0;
            while (true)
            {
                ReadOnlyMemory<CodeColor> guess = InputCode($"Input guess #{++breakerAttemptsCount}: ");
                Response response = InputResponse();
                builder.AddResponse(guess, response);

                SolutionBuilder<CodeColor>.SolutionsAnalysis analysis = builder.AnalyzeSolutions(CancellationToken.None);
                analysis.ApplyAnalysisBackToBuilder();
                if (analysis.ViableSolutionsFound == 1)
                {
                    Console.WriteLine("Solution found!");
                    break;
                }

                Console.WriteLine($"{analysis.ViableSolutionsFound} solutions remaining.");

                PrintProbabilities(analysis);
                PrintSuggestedGuess(builder, CancellationToken.None);
            }
        }

        private static void PrintSuggestedGuess(SolutionBuilder<CodeColor> builder, CancellationToken cancellationToken)
        {
            Scenario<CodeColor>? scenario = builder.GetProbableSolution(cancellationToken);
            Console.WriteLine("A reasonable next guess: {0}", string.Join(", ", scenario.NodeStates));
        }

        private static void PrintProbabilities(SolutionBuilder<CodeColor>.SolutionsAnalysis analysis)
        {
            // Sample grid printout:
            //
            //                 1    2    3     4
            // Magenta      100%  80%
            // Purple         0%  20%
            // Yellow         0%   0%
            // Teal
            // White
            // Orange
            string[] colorNames = Enum.GetNames(typeof(CodeColor));
            int maxColorLength = colorNames.Select(n => n.Length).Max();
            const int positionColumnWidth = 5;

            // Print header row with card names
            Console.Write(new string(' ', maxColorLength + 1));
            for (int position = 1; position <= Rules.CodeSize; position++)
            {
                Console.Write($"{position,-positionColumnWidth}");
            }

            Console.WriteLine();

            for (int i = 0; i < Rules.ColorCount; i++)
            {
                Console.Write("{0,-" + (maxColorLength + 1) + "}", colorNames[i]);
                for (int j = 0; j < Rules.CodeSize; j++)
                {
                    int percent = (int)(analysis.GetNodeValueCount(j, (CodeColor)i) * 100 / analysis.ViableSolutionsFound);
                    string percentWithUnits = percent.ToString(CultureInfo.CurrentCulture) + "%";
                    Console.Write($"{percentWithUnits,-positionColumnWidth}");
                }

                Console.WriteLine();
            }
        }

        private static bool TryParseCode(string input, Span<CodeColor> code)
        {
            if (GuessPattern.IsMatch(input))
            {
                for (int i = 0; i < code.Length; i++)
                {
                    code[i] = CodeColorsByFirstLetter[char.ToUpper(input[i], CultureInfo.CurrentCulture)];
                }

                return true;
            }

            Console.WriteLine("Invalid input. Specify four characters, each representing the first letter of a color.");
            return false;
        }

        private static ReadOnlyMemory<CodeColor> InputCode(string prompt)
        {
            var guess = new CodeColor[Rules.CodeSize];
            while (true)
            {
                Console.Write($"{prompt} ({ColorChoices}): ");
                string guessLine = Console.ReadLine();
                if (TryParseCode(guessLine, guess))
                {
                    return guess;
                }
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
