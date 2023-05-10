// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using CondenseSkillLib;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace PRSkill;

public static class FunctionEx
{
    public static async Task<SKContext> RollingChunkProcess(
        this ISKFunction func, 
        List<string> chunkedInput, 
        SKContext context)
    {
        context.Variables.Set("previousresults", string.Empty);
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            context.Variables.Set("previousresults", context.Variables.ToString());
        }

        return context;
    }

    public static async Task<SKContext> CondenseChunkProcess(
        this ISKFunction func, 
        CondenseSkill condenseSkill, 
        List<string> chunkedInput, 
        SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            results.Add(context.Variables.ToString());
        }

        if (chunkedInput.Count <= 1)
        {
            context.Variables.Update(context.Variables.ToString());
            return context;
        }

        // update memory with serialized list of results
        context.Variables.Update(string.Join(CondenseSkill.RESULTS_SEPARATOR, results));
        return await condenseSkill.Condense(context);
    }

    public static async Task<SKContext> AggregateChunkProcess(
        this ISKFunction func, 
        List<string> chunkedInput, 
        SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            results.Add(context.Variables.ToString());
        }

        context.Variables.Update(string.Join("\n", results));
        return context;
    }
}
