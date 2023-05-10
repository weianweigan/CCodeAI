// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CondenseSkillLib;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using PRSkill.Utils;

namespace PRSkill;

public class PullRequestSkill
{
    public const string SEMANTIC_FUNCTION_PATH = "PRSkill";
    private const int CHUNK_SIZE = 2000; // Eventually this should come from the kernel

    private readonly CondenseSkill condenseSkill;

    public PullRequestSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = Path.Combine(
            Path.GetDirectoryName(typeof(SkillsProvider).Assembly.Location),
            "GitSkills");
            var PRSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
            this.condenseSkill = new CondenseSkill(kernel);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction(description: "Generate feedback for a pull request based on a git diff or git show file output.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GeneratePullRequestFeedback(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GeneratePullRequestFeedback called");

            var prFeedbackGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestFeedbackGenerator");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prFeedbackGenerator.AggregateChunkProcess(chunkedInput, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Generate a commit message based on a git diff file output.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` command.")]
    public async Task<SKContext> GenerateCommitMessage(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GenerateCommitMessage called");

            var commitGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await commitGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Generate a pull request description based on a git diff or git show file output using a rolling query mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GeneratePR_Rolling(SKContext context)
    {
        try
        {
            var prGenerator_Rolling = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator_Rolling");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prGenerator_Rolling.RollingChunkProcess(chunkedInput, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Generate a pull request description based on a git diff or git show file output using a reduce mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GeneratePR(SKContext context)
    {
        try
        {
            var prGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }
}
