// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        var scenario = GetScenario(Orange, null, Magenta, Teal);
        Assert.False(constraint.Resolve(scenario));
        Assert.Null(scenario[1]);
    }

    [Fact]
    public void Resolve_TwoRedsAndAWhite()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 2, WhiteCount = 1 });

        var scenario = GetScenario(Orange, null, Magenta, Teal);
        Assert.True(constraint.Resolve(scenario));
        Assert.Equal(Yellow, scenario[1]);

        Assert.False(constraint.Resolve(new Scenario<CodeColor>(Rules.Nodes)));
    }

    [Fact]
    public void Resolve_ThreeRedsAndAWhite()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 3, WhiteCount = 1 });
        var scenario = GetScenario(Orange, null, Yellow, Purple);
        Assert.True(constraint.Resolve(scenario));
        Assert.Equal(Yellow, scenario[1]);
    }

    [Fact]
    public void Resolve_ThreeReds()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 3 });
        var scenario = GetScenario(Orange, null, Magenta, Purple);
        Assert.True(constraint.Resolve(scenario));
        Assert.Equal(Yellow, scenario[1]);
    }

    [Fact]
    public void GetState_3Red1White_Breakable()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 3, WhiteCount = 1 });
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable, constraint.GetState(GetScenario(Orange, Yellow, null, null)));
    }

    [Fact]
    public void GetState_3Red1White_Broken()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { RedCount = 3, WhiteCount = 1 });
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Orange, Yellow, Magenta, null)));
    }

    [Fact]
    public void GetState_4White_Broken()
    {
        var constraint = new ResponseConstraint(new[] { Orange, Yellow, Teal, Purple }, new Response { WhiteCount = 4 });
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(Magenta, null, null, null)));
    }

    [Fact, Trait("Invalid", "true")]
    public void GetState_4White_AllOneColor()
    {
        // When the guess is all one color, and the response says that the right colors are used but in the wrong positions,
        // that's a contradiction because 1 color cannot be rearranged.
        // This should immediately be seen as a broken game, even with an empty guess.
        var constraint = new ResponseConstraint(new[] { Yellow, Yellow, Yellow, Yellow }, new Response { WhiteCount = 4 });
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(null, null, null, null)));
    }

    [Fact, Trait("Invalid", "true")]
    public void GetState_4White_TwoColors()
    {
        // When the colors are all right but the positions are all wrong, yet we have 3 of one color and 1 of another,
        // that's a contradiction because there are only 3 other possible permutations and they all involve two positions remainin the same,
        // so we ought to have 2 red pins if the answer really has 3 yellows and 1 orange.
        // This should immediately be seen as a broken game, even with an empty guess.
        var constraint = new ResponseConstraint(new[] { Yellow, Yellow, Orange, Yellow }, new Response { WhiteCount = 4 });
        Assert.Equal(ConstraintStates.None, constraint.GetState(GetScenario(null, null, null, null)));
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

        // There are two indeterminate nodes, but a white pin would be required, so it's broken.
        result = constraint.GetState(GetScenario(White, Orange, null, null));
        Assert.Equal(ConstraintStates.None, result);

        // There are two indeterminate nodes, so it can be satisfied.
        result = constraint.GetState(GetScenario(White, Magenta, null, null));
        Assert.Equal(ConstraintStates.Satisfiable | ConstraintStates.Breakable | ConstraintStates.Resolvable, result);

        // One node matches, one is indeterminate.
        result = constraint.GetState(GetScenario(Orange, null, Magenta, Orange));
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

    [Fact]
    public void GetState_RejectsSolutionWithNoColorFromOneColorMatchingClue()
    {
        var constraint = new ResponseConstraint(new[] { White, Teal, Purple, Magenta }, new Response { WhiteCount = 1 });

        // The proposed solution is invalid because it does not contain *any* color from the clue already obtained,
        // yet that clue specified that exactly ONE color was correct, simply out of place.
        Assert.Equal(ConstraintStates.Resolved, constraint.GetState(GetScenario(Yellow, Orange, Yellow, Orange)));
    }

    /// <summary>
    /// For some random code, enumerate every single guess, calculate an appropriate response with <see cref="Rules.CreateResponse(ReadOnlySpan{CodeColor}, ReadOnlySpan{CodeColor})"/>,
    /// and verify that our constraint considers the correct solution to be compatible with that response.
    /// </summary>
    [Fact]
    public void ExhaustiveSatisfiedResolvedScenarioComparisons()
    {
        ReadOnlyMemory<CodeColor> code = Rules.MakeCode();
        var guess = new CodeColor[Rules.CodeSize];
        foreach (bool combination in EnumerateAllSolutions(guess.AsMemory()))
        {
            Response response = Rules.CreateResponse(guess, code.Span);
            var constraint = new ResponseConstraint(guess, response);
            Assert.Equal(ConstraintStates.Resolved | ConstraintStates.Satisfied, constraint.GetState(GetScenario(code.Span)));
        }
    }

    private static IEnumerable<bool> EnumerateAllSolutions(Memory<CodeColor> memory)
    {
        for (int position = 0; position < memory.Length; position++)
        {
            for (int colorIndex = 0; colorIndex < Rules.ColorCount; colorIndex++)
            {
                memory.Span[position] = (CodeColor)colorIndex;

                if (memory.Length > 1)
                {
                    foreach (bool combination in EnumerateAllSolutions(memory.Slice(1)))
                    {
                        yield return true;
                    }
                }
                else
                {
                    yield return true;
                }
            }
        }
    }

    private static Scenario<CodeColor> GetScenario(ReadOnlySpan<CodeColor> content)
    {
        var span = new CodeColor?[content.Length];
        for (int i = 0; i < content.Length; i++)
        {
            span[i] = content[i];
        }

        return GetScenario(span);
    }

    private static Scenario<CodeColor> GetScenario(ReadOnlySpan<CodeColor?> content)
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

    [DebuggerStepThrough]
    private static Scenario<CodeColor> GetScenario(params CodeColor?[] content) => GetScenario(content.AsSpan());
}
