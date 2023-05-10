// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace CondenseSkillLib;

public class CondenseSkill
{
    public static readonly string RESULTS_SEPARATOR = string.Format("\n====={0}=====\n", "EndResult");
    public const string SEMANTIC_FUNCTION_PATH = "CondenseSkill";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel
    public CondenseSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = Path.Combine(
            Path.GetDirectoryName(typeof(SkillsProvider).Assembly.Location),
            "GitSkills");
            var condenseSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction(description: "Condense multiple chunks of text into a single chunk.")]
    [SKFunctionContextParameter(Name = "Input", Description = "String of text that contains multiple chunks of similar formatting, style, and tone.")]
    public async Task<SKContext> Condense(SKContext context)
    {
        try
        {
            var condenser = context.Func(SEMANTIC_FUNCTION_PATH, "Condenser");

            var input = context.Variables.Input;

            List<string> lines = TextChunker.SplitPlainTextLines(input, CHUNK_SIZE);
            List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, CHUNK_SIZE);

            var condenseResult = new List<string>();
            foreach (var paragraph in paragraphs)
            {
                context.Variables.Update(paragraph + RESULTS_SEPARATOR);
                context = await condenser.InvokeAsync(context);
                condenseResult.Add(context.Result);
            }

            if (paragraphs.Count <= 1)
            {
                return context;
            }

            // update memory with serialized list of results and call condense again
            context.Variables.Update(string.Join("\n", condenseResult));
            context.Log.LogWarning($"Condensing {paragraphs.Count} paragraphs");
            return await Condense(context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }
}
