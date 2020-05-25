// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using MasterMind;
using Nerdbank.Algorithms.NodeConstraintSelection;
using Xunit;
using Xunit.Abstractions;
using static MasterMind.CodeColor;

public class ActualGameTests : TestBase, IDisposable
{
    private readonly SolutionBuilder<CodeColor> builder = Rules.CreateSolutionBuilder();
    private readonly List<(ReadOnlyMemory<CodeColor> Guess, Response Response)> responses = new List<(ReadOnlyMemory<CodeColor>, Response)>();
    private SolutionBuilder<CodeColor>.SolutionsAnalysis? analysis;

    public ActualGameTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

    public void Dispose()
    {
        this.analysis = this.builder.AnalyzeSolutions(CancellationToken.None);
        Assert.Equal(1, this.analysis.ViableSolutionsFound);
        this.analysis.ApplyAnalysisBackToBuilder();

        // Verify that each response given matches what we would have given.
        Span<CodeColor> solution = stackalloc CodeColor[Rules.CodeSize];
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            solution[i] = this.builder[i]!.Value;
        }

        foreach (var entry in this.responses)
        {
            var ourResponse = Rules.CreateResponse(entry.Guess.Span, solution);
            Assert.Equal(entry.Response, ourResponse);
        }
    }

    [Fact]
    public void GameScript1()
    {
        this.AddResponse(new[] { Magenta, White, Teal, Yellow }, new Response { WhiteCount = 2 });
        this.AddResponse(new[] { Purple, Teal, Orange, Magenta }, default);
        this.AddResponse(new[] { Yellow, White, Yellow, White }, new Response { RedCount = 3 });
        this.builder.AddResponse(new[] { Yellow, Yellow, Yellow, White }, new Response { RedCount = 4 });
    }

    [Fact]
    public void GameScript2()
    {
        this.AddResponse(new[] { Purple, Magenta, Yellow, Orange }, new Response { RedCount = 1 });
        this.AddResponse(new[] { Teal, White, Teal, Orange }, default);
        this.AddResponse(new[] { Purple, Purple, Purple, Purple }, default);
        this.AddResponse(new[] { Magenta, Magenta, Magenta, Magenta }, default);
    }

    [Fact]
    public void GameScript3()
    {
        this.AddResponse(new[] { Purple, White, Yellow, Teal }, new Response { WhiteCount = 3 });
        this.AddResponse(new[] { Yellow, Purple, White, Magenta }, new Response { RedCount = 2 });
        this.AddResponse(new[] { Teal, Purple, White, Orange }, new Response { RedCount = 3 });
        this.AddResponse(new[] { Teal, Purple, White, White }, new Response { RedCount = 3 });
        this.AddResponse(new[] { Teal, Purple, White, Purple }, new Response { RedCount = 4 });
    }

    [Fact]
    public void GameScript4()
    {
        this.AddResponse(new[] { White, Yellow, Teal, Orange }, new Response { RedCount = 1, WhiteCount = 1 });
        this.AddResponse(new[] { White, Teal, Purple, Magenta }, new Response { WhiteCount = 1 });
        this.AddResponse(new[] { Orange, White, Teal, Orange }, new Response { RedCount = 1 });
        this.AddResponse(new[] { Yellow, Yellow, Yellow, Orange }, new Response { RedCount = 3 });
        this.AddResponse(new[] { Yellow, Purple, Yellow, Orange }, new Response { RedCount = 3 });
        this.AddResponse(new[] { Yellow, Magenta, Yellow, Orange }, new Response { RedCount = 4 });
    }

    private void AddResponse(ReadOnlyMemory<CodeColor> guess, Response response)
    {
        this.responses.Add((guess, response));
        this.builder.AddResponse(guess, response);
        this.analysis = this.builder.AnalyzeSolutions(CancellationToken.None);
        this.analysis.ApplyAnalysisBackToBuilder();
        this.PrintPossibleSolutionsSimple(this.analysis);
    }
}
