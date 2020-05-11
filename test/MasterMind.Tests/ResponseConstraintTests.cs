// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Drawing;
using MasterMind;
using Nerdbank.Algorithms.NodeConstraintSelection;
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

    [Fact]
    public void Equality_Object()
    {
        var constraint1a = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        var constraint1b = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        var constraint2 = new ResponseConstraint(new CodeColor[Rules.CodeSize], new Response { RedCount = 2 });
        Assert.True(constraint1a.Equals((object)constraint1b));
        Assert.False(constraint1a.Equals((object)constraint2));
    }

    [Fact]
    public void GetHashCode_Test()
    {
        var constraint1a = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        var constraint1b = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        var constraint2 = new ResponseConstraint(new CodeColor[Rules.CodeSize], new Response { RedCount = 2 });
        Assert.Equal(constraint1a.GetHashCode(), constraint1b.GetHashCode());
        Assert.NotEqual(constraint1a.GetHashCode(), constraint2.GetHashCode());
    }

    [Fact]
    public void Resolve_TwoReds()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 2 });

        var scenario = GetScenario(Orange, null, Purple, Teal);
        Assert.True(constraint.Resolve(scenario));
        Assert.Equal(Yellow, scenario[1]);

        Assert.False(constraint.Resolve(new Scenario<CodeColor>(Rules.Nodes)));
    }

    [Fact]
    public void Resolve_ThreeReds()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 3 });
        var scenario = GetScenario(Orange, null, Yellow, Purple);
        Assert.True(constraint.Resolve(scenario));
        Assert.Equal(Yellow, scenario[1]);
    }

    [Fact]
    public void GetState_TwoReds()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 2 });

        // Three exact matches is an invalid solution.
        var result = constraint.GetState(GetScenario(Orange, Yellow, White, Purple));
        Assert.Equal(ConstraintStates.Resolved, result);

        // Only one position is the same as the original guess, so it is not satisfiable.
        result = constraint.GetState(GetScenario(White, Orange, Yellow, Purple));
        Assert.Equal(ConstraintStates.Resolved, result);

        // There are two indeterminate nodes, so it can be satisfied.
        result = constraint.GetState(GetScenario(White, Orange, null, null));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable | ConstraintStates.Resolvable, result);

        // One node matches, one is indeterminate.
        result = constraint.GetState(GetScenario(Orange, null, Purple, Teal));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable | ConstraintStates.Resolvable, result);
    }

    [Fact]
    public void GetState_ZeroMarkers()
    {
        var constraint = new ResponseConstraint(new[] { Purple, Teal, Orange, Magenta }, default);
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Purple, null, null, null)));
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Teal, null, null, null)));
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Orange, null, null, null)));
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Magenta, null, null, null)));

        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(Yellow, null, null, null)));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(White, null, null, null)));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(White, White, White, null)));
        Assert.Equal(ConstraintStates.Satisfied | ConstraintStates.Resolved, constraint.GetState(GetScenario(White, White, White, White)));
    }

    private static Scenario<CodeColor> GetScenario(params CodeColor?[] content)
    {
        var scenario = new Scenario<CodeColor>(Rules.Nodes);
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] is CodeColor)
            {
                scenario[i] = content[i];
            }
        }

        return scenario;
    }
}
