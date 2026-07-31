// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MasterMind;
using Xunit;

public class ResponseTests
{
    public ResponseTests(ITestOutputHelper logger)
    {
        _ = logger;
    }

    [Fact]
    public void Equals_Tests()
    {
        Response defaultResponse = default;
        Assert.Equal(defaultResponse, defaultResponse);
        Response responseWithRed = new Response { RedCount = 3 };
        Assert.NotEqual(defaultResponse, responseWithRed);
        Response responseWithWhite = new Response { WhiteCount = 3 };
        Assert.NotEqual(defaultResponse, responseWithWhite);
        Assert.NotEqual(responseWithRed, responseWithWhite);
    }

    [Fact]
    public void Equals_Object_Tests()
    {
        Response defaultResponse = default;
        Response responseWithRed = new Response { RedCount = 3 };
        Assert.True(defaultResponse.Equals((object)defaultResponse));
        Assert.False(defaultResponse.Equals((object)responseWithRed));
    }

    [Fact]
    public void EqualityOperators()
    {
        Response defaultResponse = default;
        Response responseWithRed = new Response { RedCount = 3 };
        Assert.True(defaultResponse == default);
        Assert.False(defaultResponse != default);
        Assert.False(defaultResponse == responseWithRed);
        Assert.True(defaultResponse != responseWithRed);
    }

    [Fact]
    public void GetHashCode_Tests()
    {
        Response defaultResponse = default;
        Response responseWithRed = new Response { RedCount = 3 };
        Response responseWithWhite = new Response { WhiteCount = 3 };
        Assert.NotEqual(defaultResponse.GetHashCode(), responseWithRed.GetHashCode());
        Assert.NotEqual(defaultResponse.GetHashCode(), responseWithWhite.GetHashCode());
    }
}
