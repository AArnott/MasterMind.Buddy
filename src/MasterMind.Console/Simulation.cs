// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Nerdbank.Algorithms.NodeConstraintSelection;
using static MasterMind.CodeColor;

namespace MasterMind.Console;

/// <summary>
/// Fully automated MasterMind games: random CodeMaker vs strategy-driven CodeBreaker.
/// Subsequent guesses after the opening move use <see cref="Rules.SuggestGuess(IReadOnlyList{int})"/>.
/// </summary>
internal static class Simulation
{
    /// <summary>
    /// Standard MasterMind turn limit; exceeding it is a CodeMaker win.
    /// </summary>
    public const int MaxGuesses = 10;

    /// <summary>
    /// Default number of games to play per opening strategy.
    /// </summary>
    public const int DefaultGamesPerStrategy = 100;

    /// <summary>
    /// Runs the multi-strategy opening-move benchmark and prints a report.
    /// </summary>
    /// <param name="gamesPerStrategy">Games to play for each strategy.</param>
    /// <param name="seed">Optional RNG seed for reproducible secret codes.</param>
    public static void Run(int gamesPerStrategy = DefaultGamesPerStrategy, int? seed = null)
    {
        int effectiveSeed = seed ?? Environment.TickCount;
        Random random = new(effectiveSeed);

        // Shared secret set so strategies face the same CodeMaker codes.
        CodeColor[][] secrets = GenerateSecrets(gamesPerStrategy, random);

        OpeningStrategy[] strategies = CreateStrategies();
        StrategyResult[] results = new StrategyResult[strategies.Length];

        System.Console.WriteLine("MasterMind opening-move simulation");
        System.Console.WriteLine($"  Games per strategy : {gamesPerStrategy}");
        System.Console.WriteLine($"  Max guesses        : {MaxGuesses}");
        System.Console.WriteLine($"  RNG seed           : {effectiveSeed}");
        System.Console.WriteLine($"  Subsequent guesses : Rules.SuggestGuess (minimax)");
        System.Console.WriteLine();

        Stopwatch total = Stopwatch.StartNew();
        for (int s = 0; s < strategies.Length; s++)
        {
            OpeningStrategy strategy = strategies[s];
            System.Console.Write($"Running {strategy.Name} ({s + 1}/{strategies.Length})...");
            System.Console.Out.Flush();

            Stopwatch sw = Stopwatch.StartNew();
            results[s] = RunStrategy(strategy, secrets);
            sw.Stop();

            System.Console.WriteLine(
                $" done in {sw.Elapsed.TotalSeconds:F1}s — " +
                $"win {results[s].Wins}/{gamesPerStrategy}, " +
                $"avg guesses (wins) {results[s].AverageGuessesWhenWon:F2}");
        }

        total.Stop();
        System.Console.WriteLine();
        System.Console.WriteLine($"All strategies finished in {total.Elapsed.TotalSeconds:F1}s.");
        System.Console.WriteLine();
        PrintReport(results, gamesPerStrategy, effectiveSeed);
    }

    private static OpeningStrategy[] CreateStrategies()
    {
        // Precompute the library's own first-move recommendation (full code space).
        List<int> fullSpace = new(Rules.CodeSpaceSize);
        for (int i = 0; i < Rules.CodeSpaceSize; i++)
        {
            fullSpace.Add(i);
        }

        int? minimaxPacked = Rules.SuggestGuess(fullSpace);
        CodeColor[] minimaxOpening = UnpackRequired(minimaxPacked);

        return new[]
        {
            new OpeningStrategy(
                "FourDistinct",
                "ABCD — four different colors (max color coverage).",
                _ => new[] { Magenta, Purple, Yellow, Teal }),
            new OpeningStrategy(
                "KnuthAABC",
                "AABC — two of one color + two singles (classic Knuth-style opener).",
                _ => new[] { Magenta, Magenta, Purple, Yellow }),
            new OpeningStrategy(
                "TwoPairs",
                "AABB — two pairs of colors.",
                _ => new[] { Magenta, Magenta, Purple, Purple }),
            new OpeningStrategy(
                "Alternating",
                "ABAB — alternating two colors.",
                _ => new[] { Magenta, Purple, Magenta, Purple }),
            new OpeningStrategy(
                "ThreeOne",
                "AAAB — three of one color + one other.",
                _ => new[] { Magenta, Magenta, Magenta, Purple }),
            new OpeningStrategy(
                "AllSame",
                "AAAA — all four positions the same color.",
                _ => new[] { Magenta, Magenta, Magenta, Magenta }),
            new OpeningStrategy(
                "Minimax",
                $"Library SuggestGuess on empty board: {FormatCode(minimaxOpening)}.",
                _ => (CodeColor[])minimaxOpening.Clone()),
            new OpeningStrategy(
                "Random",
                "Uniform random opening each game (baseline).",
                rng =>
                {
                    CodeColor[] code = new CodeColor[Rules.CodeSize];
                    for (int i = 0; i < code.Length; i++)
                    {
                        code[i] = (CodeColor)rng.Next(Rules.ColorCount);
                    }

                    return code;
                }),
        };
    }

    private static CodeColor[][] GenerateSecrets(int count, Random random)
    {
        CodeColor[][] secrets = new CodeColor[count][];
        for (int i = 0; i < count; i++)
        {
            CodeColor[] secret = new CodeColor[Rules.CodeSize];
            for (int p = 0; p < Rules.CodeSize; p++)
            {
                secret[p] = (CodeColor)random.Next(Rules.ColorCount);
            }

            secrets[i] = secret;
        }

        return secrets;
    }

    private static StrategyResult RunStrategy(OpeningStrategy strategy, CodeColor[][] secrets)
    {
        // Separate RNG so Random-opening strategy is independent of secret generation.
        Random strategyRng = new(HashCode.Combine(strategy.Name.GetHashCode(StringComparison.Ordinal), secrets.Length));

        int[] guessCounts = new int[secrets.Length];
        int wins = 0;
        int totalGuessesOnWins = 0;
        int sumGuesses = 0;
        int maxGuessesSeen = 0;
        int[] histogram = new int[MaxGuesses + 2]; // index 1..MaxGuesses wins; MaxGuesses+1 = losses

        for (int g = 0; g < secrets.Length; g++)
        {
            CodeColor[] opening = strategy.GetOpening(strategyRng);
            int guesses = PlayGame(secrets[g], opening);
            guessCounts[g] = guesses;
            sumGuesses += guesses;
            if (guesses > maxGuessesSeen)
            {
                maxGuessesSeen = guesses;
            }

            if (guesses <= MaxGuesses)
            {
                wins++;
                totalGuessesOnWins += guesses;
                histogram[guesses]++;
            }
            else
            {
                histogram[MaxGuesses + 1]++;
            }
        }

        return new StrategyResult(
            strategy,
            secrets.Length,
            wins,
            totalGuessesOnWins,
            sumGuesses,
            maxGuessesSeen,
            histogram,
            guessCounts);
    }

    /// <summary>
    /// Plays one automated game. Returns the number of guesses used to crack the code,
    /// or <see cref="MaxGuesses"/> + 1 if the breaker fails within the limit.
    /// </summary>
    private static int PlayGame(ReadOnlySpan<CodeColor> secret, CodeColor[] opening)
    {
        SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();

        // Opening move (strategy-chosen).
        Response response = Rules.CreateResponse(opening, secret);
        if (response.RedCount == Rules.CodeSize)
        {
            return 1;
        }

        builder.AddResponse(opening, response);

        Span<CodeColor> guess = stackalloc CodeColor[Rules.CodeSize];
        for (int attempt = 2; attempt <= MaxGuesses; attempt++)
        {
            List<int> remaining = Rules.GetRemainingPackedSolutions(builder);
            if (remaining.Count == 0)
            {
                // Inconsistent state should not happen against a valid secret.
                return MaxGuesses + 1;
            }

            int? packed = Rules.SuggestGuess(remaining);
            if (packed is null)
            {
                return MaxGuesses + 1;
            }

            Rules.UnpackCode(packed.Value, guess);
            response = Rules.CreateResponse(guess, secret);
            if (response.RedCount == Rules.CodeSize)
            {
                return attempt;
            }

            // Materialize guess for the constraint (builder stores the memory).
            CodeColor[] guessArray = guess.ToArray();
            builder.AddResponse(guessArray, response);
        }

        return MaxGuesses + 1;
    }

    private static void PrintReport(StrategyResult[] results, int gamesPerStrategy, int seed)
    {
        // Rank primarily by win rate, then by average guesses when won, then by overall avg.
        StrategyResult[] ranked = results
            .OrderByDescending(r => r.Wins)
            .ThenBy(r => r.AverageGuessesWhenWon)
            .ThenBy(r => r.AverageGuessesAll)
            .ToArray();

        System.Console.WriteLine("=== Ranking (best first) ===");
        System.Console.WriteLine();
        System.Console.WriteLine(
            $"{"#",-3} {"Strategy",-14} {"Win%",-8} {"Wins",-10} {"Avg(win)",-10} {"Avg(all)",-10} {"Max",-5} {"Opening"}");
        System.Console.WriteLine(new string('-', 100));

        for (int i = 0; i < ranked.Length; i++)
        {
            StrategyResult r = ranked[i];
            CodeColor[] sampleOpening = r.Strategy.GetOpening(new Random(0));
            System.Console.WriteLine(
                $"{i + 1,-3} {r.Strategy.Name,-14} " +
                $"{r.WinRate * 100,5:F1}%  " +
                $"{r.Wins}/{r.Games,-6} " +
                $"{r.AverageGuessesWhenWon,8:F3}  " +
                $"{r.AverageGuessesAll,8:F3}  " +
                $"{r.MaxGuessesSeen,3}  " +
                $"{FormatCode(sampleOpening)}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("=== Strategy details ===");
        foreach (StrategyResult r in ranked)
        {
            System.Console.WriteLine();
            System.Console.WriteLine($"{r.Strategy.Name}: {r.Strategy.Description}");
            System.Console.WriteLine($"  Wins: {r.Wins}/{r.Games} ({r.WinRate * 100:F1}%)");
            System.Console.WriteLine($"  Avg guesses when won: {r.AverageGuessesWhenWon:F3}");
            System.Console.WriteLine($"  Avg guesses (losses counted as {MaxGuesses + 1}): {r.AverageGuessesAll:F3}");
            System.Console.WriteLine($"  Guess histogram (wins by length; L=loss): {FormatHistogram(r.Histogram)}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("=== Summary ===");
        StrategyResult best = ranked[0];
        System.Console.WriteLine(
            $"Best opening under this protocol: {best.Strategy.Name} " +
            $"({FormatCode(best.Strategy.GetOpening(new Random(0)))}) — " +
            $"{best.WinRate * 100:F1}% wins, avg {best.AverageGuessesWhenWon:F3} guesses when won.");

        // Pairwise note: how much better than random baseline.
        StrategyResult? randomResult = results.FirstOrDefault(r => r.Strategy.Name == "Random");
        if (randomResult is not null && !ReferenceEquals(best, randomResult))
        {
            double deltaAvg = randomResult.AverageGuessesWhenWon - best.AverageGuessesWhenWon;
            System.Console.WriteLine(
                $"Vs Random baseline: {deltaAvg:+0.000;-0.000;0} fewer guesses on wins, " +
                $"{(best.WinRate - randomResult.WinRate) * 100:+0.0;-0.0;0} pp win rate.");
        }

        System.Console.WriteLine();
        System.Console.WriteLine($"Re-run with: dotnet run --project src/MasterMind.Console -- simulate {gamesPerStrategy} {seed}");
    }

    private static string FormatHistogram(int[] histogram)
    {
        StringBuilder sb = new();
        for (int g = 1; g <= MaxGuesses; g++)
        {
            if (histogram[g] > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(CultureInfo.InvariantCulture, $"{g}:{histogram[g]}");
            }
        }

        if (histogram[MaxGuesses + 1] > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(CultureInfo.InvariantCulture, $"L:{histogram[MaxGuesses + 1]}");
        }

        return sb.Length > 0 ? sb.ToString() : "(empty)";
    }

    private static string FormatCode(ReadOnlySpan<CodeColor> code)
    {
        // Compact first-letter form matching the interactive console (e.g. MPYT).
        Span<char> chars = stackalloc char[code.Length];
        for (int i = 0; i < code.Length; i++)
        {
            chars[i] = code[i].ToString()![0];
        }

        return new string(chars);
    }

    private static CodeColor[] UnpackRequired(int? packed)
    {
        if (packed is null)
        {
            throw new InvalidOperationException("SuggestGuess returned null on the full code space.");
        }

        CodeColor[] code = new CodeColor[Rules.CodeSize];
        Rules.UnpackCode(packed.Value, code);
        return code;
    }

    private sealed class OpeningStrategy
    {
        public OpeningStrategy(string name, string description, Func<Random, CodeColor[]> getOpening)
        {
            this.Name = name;
            this.Description = description;
            this.GetOpening = getOpening;
        }

        public string Name { get; }

        public string Description { get; }

        public Func<Random, CodeColor[]> GetOpening { get; }
    }

    private sealed class StrategyResult
    {
        public StrategyResult(
            OpeningStrategy strategy,
            int games,
            int wins,
            int totalGuessesOnWins,
            int sumGuesses,
            int maxGuessesSeen,
            int[] histogram,
            int[] guessCounts)
        {
            this.Strategy = strategy;
            this.Games = games;
            this.Wins = wins;
            this.TotalGuessesOnWins = totalGuessesOnWins;
            this.SumGuesses = sumGuesses;
            this.MaxGuessesSeen = maxGuessesSeen;
            this.Histogram = histogram;
            this.GuessCounts = guessCounts;
        }

        public OpeningStrategy Strategy { get; }

        public int Games { get; }

        public int Wins { get; }

        public int TotalGuessesOnWins { get; }

        public int SumGuesses { get; }

        public int MaxGuessesSeen { get; }

        public int[] Histogram { get; }

        public int[] GuessCounts { get; }

        public double WinRate => this.Games == 0 ? 0 : (double)this.Wins / this.Games;

        public double AverageGuessesWhenWon =>
            this.Wins == 0 ? double.NaN : (double)this.TotalGuessesOnWins / this.Wins;

        public double AverageGuessesAll => this.Games == 0 ? double.NaN : (double)this.SumGuesses / this.Games;
    }
}
