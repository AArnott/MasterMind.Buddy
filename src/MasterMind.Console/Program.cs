// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;
using Nerdbank.Algorithms.NodeConstraintSelection;

namespace MasterMind.Console;

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
        System.Console.Write("What role are you playing (M = Code _Maker, B = Code _Breaker)? ");
        while (true)
        {
            switch (char.ToUpper(System.Console.ReadKey().KeyChar, CultureInfo.CurrentCulture))
            {
                case 'M':
                    System.Console.WriteLine("Code maker");
                    CodeMaker();
                    return;
                case 'B':
                    System.Console.WriteLine("Code breaker");
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
            System.Console.Write("Enter code (or leave blank to generate one): ");
            string? codeLine = System.Console.ReadLine();
            if (string.IsNullOrEmpty(codeLine))
            {
                Random random = new();
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

        System.Console.WriteLine("Your code is: {0}", string.Join(", ", code));

        int breakerAttemptsCount = 0;
        while (true)
        {
            ReadOnlySpan<CodeColor> guess = InputCode("What is the code breaker's guess?").Span;
            Response response = Rules.CreateResponse(guess, code);
            System.Console.WriteLine($"{response.RedCount} red pins, {response.WhiteCount} white pins.");
            if (response.RedCount == Rules.CodeSize)
            {
                System.Console.WriteLine("Game over. Code breaker wins.");
                break;
            }

            if (++breakerAttemptsCount == 10)
            {
                System.Console.WriteLine("Game over. YOU win.");
                break;
            }
        }
    }

    private static void CodeBreaker()
    {
        SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();

        int breakerAttemptsCount = 0;
        while (true)
        {
            ReadOnlyMemory<CodeColor> guess = InputCode($"Input guess #{++breakerAttemptsCount}: ");
            Response response = InputResponse();
            builder.AddResponse(guess, response);

            // Enumerate the remaining code space once and reuse it for counts, probabilities, and suggestions.
            List<int> remaining = Rules.GetRemainingPackedSolutions(builder);
            if (remaining.Count == 1)
            {
                Span<CodeColor> solution = stackalloc CodeColor[Rules.CodeSize];
                Rules.UnpackCode(remaining[0], solution);
                System.Console.WriteLine("Solution found: {0}", string.Join(", ", solution.ToArray()));
                break;
            }

            if (remaining.Count == 0)
            {
                System.Console.WriteLine("No solutions remain; prior answers are inconsistent.");
                break;
            }

            System.Console.WriteLine($"{remaining.Count} solutions remaining.");

            PrintProbabilities(remaining);
            PrintSuggestedGuess(remaining, CancellationToken.None);
        }
    }

    private static void PrintSuggestedGuess(IReadOnlyList<int> remaining, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int? packedGuess = Rules.SuggestGuess(remaining);
        if (packedGuess is null)
        {
            System.Console.WriteLine("No reasonable next guess found.");
            return;
        }

        Span<CodeColor> guess = stackalloc CodeColor[Rules.CodeSize];
        Rules.UnpackCode(packedGuess.Value, guess);
        System.Console.WriteLine("A reasonable next guess: {0}", string.Join(", ", guess.ToArray()));
    }

    private static void PrintProbabilities(IReadOnlyList<int> remaining)
    {
        string[] colorNames = Enum.GetNames(typeof(CodeColor));
        int maxColorLength = colorNames.Select(n => n.Length).Max();
        const int positionColumnWidth = 5;

        Span<int> nodeValueCounts = stackalloc int[Rules.CodeSize * Rules.ColorCount];
        Rules.CountNodeValues(remaining, nodeValueCounts);

        System.Console.Write(new string(' ', maxColorLength + 1));
        for (int position = 1; position <= Rules.CodeSize; position++)
        {
            System.Console.Write($"{position,-positionColumnWidth}");
        }

        System.Console.WriteLine();

        for (int i = 0; i < Rules.ColorCount; i++)
        {
            System.Console.Write("{0,-" + (maxColorLength + 1) + "}", colorNames[i]);
            for (int j = 0; j < Rules.CodeSize; j++)
            {
                int percent = (nodeValueCounts[(j * Rules.ColorCount) + i] * 100) / remaining.Count;
                string percentWithUnits = percent.ToString(CultureInfo.CurrentCulture) + "%";
                System.Console.Write($"{percentWithUnits,-positionColumnWidth}");
            }

            System.Console.WriteLine();
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

        System.Console.WriteLine("Invalid input. Specify four characters, each representing the first letter of a color.");
        return false;
    }

    private static ReadOnlyMemory<CodeColor> InputCode(string prompt)
    {
        CodeColor[] guess = new CodeColor[Rules.CodeSize];
        while (true)
        {
            System.Console.Write($"{prompt} ({ColorChoices}): ");
            string? guessLine = System.Console.ReadLine();
            if (guessLine is not null && TryParseCode(guessLine, guess))
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
            System.Console.Write("How many red markers? ");
            if (!int.TryParse(System.Console.ReadLine(), out reds))
            {
                System.Console.WriteLine("Invalid input. Provide an integer.");
                continue;
            }

            break;
        }

        int whites;
        while (true)
        {
            System.Console.Write("How many white markers? ");
            if (!int.TryParse(System.Console.ReadLine(), out whites))
            {
                System.Console.WriteLine("Invalid input. Provide an integer.");
                continue;
            }

            break;
        }

        return new Response { RedCount = reds, WhiteCount = whites };
    }
}
