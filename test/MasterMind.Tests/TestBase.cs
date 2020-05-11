// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;
using MasterMind;
using Nerdbank.Algorithms.NodeConstraintSelection;
using Xunit.Abstractions;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper logger)
    {
        this.Logger = logger;
    }

    public ITestOutputHelper Logger { get; }

    public void PrintPossibleSolutionsSimple(SolutionBuilder<CodeColor>.SolutionsAnalysis analysis)
    {
        var stringBuilder = new StringBuilder();
        for (int i = 0; i < Rules.CodeSize; i++)
        {
            int colorPossibilities = 0;
            Span<bool> mayBeSelected = stackalloc bool[Rules.ColorCount];
            for (int colorIndex = 0; colorIndex < Rules.ColorCount; colorIndex++)
            {
                if (analysis.GetNodeValueCount(i, (CodeColor)colorIndex) > 0)
                {
                    mayBeSelected[colorIndex] = true;
                    colorPossibilities++;
                }
            }

            if (colorPossibilities > 1)
            {
                stringBuilder.Append('(');
            }

            bool firstElement = true;
            for (int j = 0; j < mayBeSelected.Length; j++)
            {
                if (mayBeSelected[j])
                {
                    if (!firstElement)
                    {
                        stringBuilder.Append(',');
                    }

                    stringBuilder.Append(Enum.GetName(typeof(CodeColor), j)!.Substring(0, 1));
                }
            }

            if (colorPossibilities > 1)
            {
                stringBuilder.Append(')');
            }

            stringBuilder.Append(' ');
        }

        stringBuilder.AppendFormat(CultureInfo.CurrentCulture, " ({0} possibilities)", analysis.ViableSolutionsFound);

        this.Logger.WriteLine(stringBuilder.ToString());
    }
}
