// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MasterMind;
using Nerdbank.Algorithms.NodeConstraintSelection;
using Xunit;
using static MasterMind.CodeColor;

public class BugRegressionTests : TestBase
{
    public BugRegressionTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Regression for https://github.com/AArnott/MasterMind.Buddy/issues/1
    /// YOYO must not remain viable after WTPM scored 0 red / 1 white.
    /// </summary>
    [Fact]
    public void Issue1_ContradictorySolutionEliminated()
    {
        SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();
        builder.AddResponse(new[] { White, Yellow, Teal, Orange }, new Response { RedCount = 1, WhiteCount = 1 });
        builder.AddResponse(new[] { White, Teal, Purple, Magenta }, new Response { RedCount = 0, WhiteCount = 1 });

        List<CodeColor[]> remaining = Rules.GetRemainingSolutions(builder);
        Assert.DoesNotContain(remaining, code => code.SequenceEqual(new[] { Yellow, Orange, Yellow, Orange }));

        // Full path from the issue should still find the true solution and never accept YOYO.
        builder.AddResponse(new[] { Orange, White, Teal, Orange }, new Response { RedCount = 1, WhiteCount = 0 });
        builder.AddResponse(new[] { Yellow, Yellow, Yellow, Orange }, new Response { RedCount = 3, WhiteCount = 0 });
        builder.AddResponse(new[] { Yellow, Purple, Yellow, Orange }, new Response { RedCount = 3, WhiteCount = 0 });
        builder.AddResponse(new[] { Yellow, Magenta, Yellow, Orange }, new Response { RedCount = 4, WhiteCount = 0 });

        remaining = Rules.GetRemainingSolutions(builder);
        Assert.Single(remaining);
        Assert.Equal(new[] { Yellow, Magenta, Yellow, Orange }, remaining[0]);
    }

    /// <summary>
    /// Regression for https://github.com/AArnott/MasterMind.Buddy/issues/2
    /// After TPWM -> 3R0W the solver should keep only consistent codes and suggest a
    /// low worst-case guess so the secret can be isolated quickly.
    /// </summary>
    [Fact]
    public void Issue2_FewerStepsWithMinimaxSuggestion()
    {
        CodeColor[] secret = new[] { Teal, Purple, Orange, Magenta };

        SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();
        CodeColor[] guess1 = new[] { Teal, Purple, White, Magenta };
        Response response1 = Rules.CreateResponse(guess1, secret);
        Assert.Equal(new Response { RedCount = 3, WhiteCount = 0 }, response1);
        builder.AddResponse(guess1, response1);

        List<CodeColor[]> remaining = Rules.GetRemainingSolutions(builder);
        Assert.Equal(20, remaining.Count);
        Assert.Contains(remaining, code => code.SequenceEqual(secret));

        // Autoplay using suggested guesses should beat the original 6-guess transcript.
        int guesses = 1;
        while (remaining.Count > 1)
        {
            CodeColor[]? suggestion = Rules.SuggestGuess(builder);
            Assert.NotNull(suggestion);
            Response response = Rules.CreateResponse(suggestion, secret);
            builder.AddResponse(suggestion, response);
            guesses++;
            remaining = Rules.GetRemainingSolutions(builder);
            this.Logger.WriteLine($"Guess {guesses}: {string.Join(",", suggestion)} -> {response}; {remaining.Count} left");
            Assert.True(guesses < 6, "Should solve issue #2 secret in fewer than 6 total guesses.");
        }

        Assert.Single(remaining);
        Assert.Equal(secret, remaining[0]);
        this.Logger.WriteLine($"Solved in {guesses} guesses.");
    }

    [Fact]
    public void SuggestGuess_SingleSolutionReturnsIt()
    {
        SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();
        CodeColor[] secret = new[] { Magenta, Purple, Yellow, Teal };
        builder.AddResponse(secret, new Response { RedCount = 4 });

        CodeColor[]? suggestion = Rules.SuggestGuess(builder);
        Assert.Equal(secret, suggestion);
    }

    [Fact]
    public void GetRemainingSolutions_MatchesAnalyzeSolutionsCount()
    {
        SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();
        builder.AddResponse(new[] { Teal, Purple, White, Magenta }, new Response { RedCount = 3, WhiteCount = 0 });

        List<CodeColor[]> remaining = Rules.GetRemainingSolutions(builder);
        SolutionBuilder<CodeColor>.SolutionsAnalysis analysis = builder.AnalyzeSolutions(CancellationToken.None);
        Assert.Equal(analysis.ViableSolutionsFound, remaining.Count);
    }
}
