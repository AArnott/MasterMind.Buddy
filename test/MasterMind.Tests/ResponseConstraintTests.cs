// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MasterMind;
using Nerdbank.Algorithms.NodeConstraintSelection;
using Xunit;
using static MasterMind.CodeColor;

public class ResponseConstraintTests
{
    [Fact]
    public void Nodes()
    {
        ResponseConstraint constraint = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
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
        ResponseConstraint constraint1a = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        ResponseConstraint constraint1b = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        ResponseConstraint constraint2 = new ResponseConstraint(new CodeColor[Rules.CodeSize], new Response { RedCount = 2 });
        ResponseConstraint constraint3 = new ResponseConstraint(new CodeColor[Rules.CodeSize] { Magenta, Yellow, White, Orange }, new Response { RedCount = 2 });
        Assert.Equal(constraint1a, constraint1b);
        Assert.NotEqual(constraint1a, constraint2);
        Assert.NotEqual(constraint1a, constraint3);
        Assert.NotEqual(constraint2, constraint3);
    }

    [Fact]
    public void Equality_Object()
    {
        ResponseConstraint constraint1a = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        ResponseConstraint constraint1b = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        ResponseConstraint constraint2 = new ResponseConstraint(new CodeColor[Rules.CodeSize], new Response { RedCount = 2 });
        Assert.True(constraint1a.Equals((object)constraint1b));
        Assert.False(constraint1a.Equals((object)constraint2));
    }

    [Fact]
    public void GetHashCode_Test()
    {
        ResponseConstraint constraint1a = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        ResponseConstraint constraint1b = new ResponseConstraint(new CodeColor[Rules.CodeSize], default);
        ResponseConstraint constraint2 = new ResponseConstraint(new CodeColor[Rules.CodeSize], new Response { RedCount = 2 });
        Assert.Equal(constraint1a.GetHashCode(), constraint1b.GetHashCode());
        Assert.NotEqual(constraint1a.GetHashCode(), constraint2.GetHashCode());
    }

    [Fact]
    public void GetState_CompleteSolution_MatchesResponse()
    {
        ResponseConstraint constraint = new ResponseConstraint(
            new[] { Magenta, Yellow, Teal, Orange },
            new Response { RedCount = 1, WhiteCount = 2 });

        ConstraintStates result = constraint.GetState(GetScenario(Magenta, Purple, Yellow, Teal));
        Assert.Equal(ConstraintStates.Satisfied | ConstraintStates.Resolved, result);
    }

    [Fact]
    public void GetState_CompleteSolution_RejectsWrongWhiteCount()
    {
        // Bug #1: YOYO must be rejected for WTPM -> 0 red, 1 white.
        ResponseConstraint constraint = new ResponseConstraint(
            new[] { White, Teal, Purple, Magenta },
            new Response { RedCount = 0, WhiteCount = 1 });

        ConstraintStates result = constraint.GetState(GetScenario(Yellow, Orange, Yellow, Orange));
        Assert.Equal(ConstraintStates.Resolved, result);
        Assert.False(result.HasFlag(ConstraintStates.Satisfiable));
    }

    [Fact]
    public void GetState_TwoReds()
    {
        ResponseConstraint constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 2 });

        // Three exact matches is an invalid solution.
        ConstraintStates result = constraint.GetState(GetScenario(Orange, Yellow, White, Purple));
        Assert.Equal(ConstraintStates.Resolved, result);
        Assert.False(result.HasFlag(ConstraintStates.Satisfiable));

        // 1 red and whites that don't match required 2R 0W.
        result = constraint.GetState(GetScenario(White, Orange, Yellow, Purple));
        Assert.Equal(ConstraintStates.Resolved, result);
        Assert.False(result.HasFlag(ConstraintStates.Satisfiable));
    }

    [Fact]
    public void GetState_ZeroMarkers()
    {
        ResponseConstraint constraint = new ResponseConstraint(new[] { Purple, Teal, Orange, Magenta }, default);
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Purple, null, null, null)));
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Teal, null, null, null)));
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Orange, null, null, null)));
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Magenta, null, null, null)));

        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(Yellow, null, null, null)));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(White, null, null, null)));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(White, White, White, null)));
        Assert.Equal(ConstraintStates.Satisfied | ConstraintStates.Resolved, constraint.GetState(GetScenario(White, White, White, White)));
    }

    [Fact]
    public void Resolve_ForcesUniqueCompletion()
    {
        // Guess TPWM scored 3 red / 0 white. With T,P already correct and position 2 known wrong,
        // position 3 must be Magenta to reach exactly three reds.
        ResponseConstraint constraint = new ResponseConstraint(
            new[] { Teal, Purple, White, Magenta },
            new Response { RedCount = 3, WhiteCount = 0 });

        Scenario<CodeColor> scenario = GetScenario(Teal, Purple, Yellow, null);
        Assert.True(constraint.GetState(scenario).HasFlag(ConstraintStates.Resolvable));
        Assert.True(constraint.Resolve(scenario));
        Assert.Equal(Magenta, scenario[3]);
    }

    [Fact]
    public void Resolve_NoForceWhenMultipleOptionsRemain()
    {
        ResponseConstraint constraint = new ResponseConstraint(
            new[] { Teal, Purple, White, Magenta },
            new Response { RedCount = 3, WhiteCount = 0 });

        Scenario<CodeColor> scenario = GetScenario(Teal, Purple, null, Magenta);
        Assert.False(constraint.GetState(scenario).HasFlag(ConstraintStates.Resolvable));
        Assert.False(constraint.Resolve(scenario));
        Assert.Null(scenario[2]);
    }

    private static Scenario<CodeColor> GetScenario(params CodeColor?[] content)
    {
        Scenario<CodeColor> scenario = new Scenario<CodeColor>(Rules.Nodes);
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
