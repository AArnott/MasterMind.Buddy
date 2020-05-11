// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MasterMind;
using Xunit;
using Xunit.Abstractions;
using static MasterMind.CodeColor;

public class RulesTests : TestBase
{
    public RulesTests(ITestOutputHelper logger)
        : base(logger)
    {
    }

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
}
