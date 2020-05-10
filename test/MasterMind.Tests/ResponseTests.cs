// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MasterMind;
using Xunit;
using Xunit.Abstractions;

public class ResponseTests
{
    private readonly ITestOutputHelper logger;

    public ResponseTests(ITestOutputHelper logger)
    {
        this.logger = logger;
    }

    [Fact]
    public void Equals_Tests()
    {
        Response defaultResponse = default;
        Assert.Equal(defaultResponse, defaultResponse);
        var responseWithRed = new Response { RedCount = 3 };
        Assert.NotEqual(defaultResponse, responseWithRed);
        var responseWithWhite = new Response { WhiteCount = 3 };
        Assert.NotEqual(defaultResponse, responseWithWhite);
        Assert.NotEqual(responseWithRed, responseWithWhite);
    }

    [Fact]
    public void Equals_Object_Tests()
    {
        Response defaultResponse = default;
        var responseWithRed = new Response { RedCount = 3 };
        Assert.True(defaultResponse.Equals((object)defaultResponse));
        Assert.False(defaultResponse.Equals((object)responseWithRed));
    }

    [Fact]
    public void EqualityOperators()
    {
        Response defaultResponse = default;
        var responseWithRed = new Response { RedCount = 3 };
        Assert.True(defaultResponse == default);
        Assert.False(defaultResponse != default);
        Assert.False(defaultResponse == responseWithRed);
        Assert.True(defaultResponse != responseWithRed);
    }

    [Fact]
    public void GetHashCode_Tests()
    {
        Response defaultResponse = default;
        var responseWithRed = new Response { RedCount = 3 };
        var responseWithWhite = new Response { WhiteCount = 3 };
        Assert.NotEqual(defaultResponse.GetHashCode(), responseWithRed.GetHashCode());
        Assert.NotEqual(defaultResponse.GetHashCode(), responseWithWhite.GetHashCode());
    }
}
