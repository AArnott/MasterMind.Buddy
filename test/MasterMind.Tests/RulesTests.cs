// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using MasterMind;
using Xunit;
using static MasterMind.CodeColor;

public class RulesTests
{
    [Fact]
    public void CreateResponse_DifferentSizes()
    {
        Assert.Throws<ArgumentException>(() => Rules.CreateResponse(new CodeColor[5], new CodeColor[2]));
    }

    [Fact]
    public void ValidResponses()
    {
        ReadOnlySpan<CodeColor> solution = new CodeColor[Rules.CodeSize]
        {
            Magenta,
            Purple,
            Yellow,
            Teal,
        };

        Response response = Rules.CreateResponse(new[] { Magenta, Yellow, Teal, Orange }, solution);
        Assert.Equal(1, response.RedCount);
        Assert.Equal(2, response.WhiteCount);

        response = Rules.CreateResponse(new[] { Magenta, White, White, Orange }, solution);
        Assert.Equal(1, response.RedCount);
        Assert.Equal(0, response.WhiteCount);

        response = Rules.CreateResponse(new[] { White, Magenta, White, Orange }, solution);
        Assert.Equal(0, response.RedCount);
        Assert.Equal(1, response.WhiteCount);

        response = Rules.CreateResponse(new[] { White, Orange, White, Orange }, solution);
        Assert.Equal(0, response.RedCount);
        Assert.Equal(0, response.WhiteCount);

        response = Rules.CreateResponse(new[] { Magenta, Purple, Yellow, Teal }, solution);
        Assert.Equal(4, response.RedCount);
        Assert.Equal(0, response.WhiteCount);
    }

    [Fact]
    public void CreateSolutionBuilder()
    {
        Assert.NotSame(Rules.CreateSolutionBuilder(), Rules.CreateSolutionBuilder());
    }

    [Fact]
    public void GameSimulation1()
    {
        Nerdbank.Algorithms.NodeConstraintSelection.SolutionBuilder<CodeColor>? builder = Rules.CreateSolutionBuilder();
        builder.AddResponse(new[] { Magenta, White, Teal, Yellow }, new Response { WhiteCount = 2 });
        builder.AddResponse(new[] { Purple, Teal, Orange, Magenta }, default(Response));
        builder.AddResponse(new[] { Yellow, White, Yellow, White }, new Response { RedCount = 3 });
        builder.AddResponse(new[] { Yellow, Yellow, Yellow, White }, new Response { RedCount = 4 });
        var analysis = builder.AnalyzeSolutions(CancellationToken.None);
        Assert.Equal(1, analysis.ViableSolutionsFound);
    }
}
