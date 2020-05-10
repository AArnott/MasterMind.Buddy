// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MasterMind;
using Xunit;
using static MasterMind.CodeColor;

public class ResponseConstraintTests
{
    [Fact]
    public void Nodes()
    {
        var constraint = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        Assert.Same(Rules.Nodes, constraint.Nodes);
    }

    [Fact]
    public void Ctor_RejectsGuessOfWrongSize()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ResponseConstraint(new CodeColor[Rules.CodeSize + 1], default));
        Assert.Equal("guess", ex.ParamName);
    }

    [Fact]
    public void Equality()
    {
        var constraint1a = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        var constraint1b = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        var constraint2 = new ResponseConstraint(new CodeColor[Rules.CodeSize], new Response { RedCount = 2 });
        var constraint3 = new ResponseConstraint(new CodeColor[Rules.CodeSize] { Magenta, Yellow, White, Orange }, new Response { RedCount = 2 });
        Assert.Equal(constraint1a, constraint1b);
        Assert.NotEqual(constraint1a, constraint2);
        Assert.NotEqual(constraint1a, constraint3);
        Assert.NotEqual(constraint2, constraint3);
    }
}
