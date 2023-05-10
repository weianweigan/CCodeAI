using CCodeAI.Extensions;
using CCodeAI.GitSkills;
using CCodeAI.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Primitives;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CCodeAI.Models;

public class WelcomeChatData:ChatData
{
    private AsyncRelayCommand<IQuickChatSkill> _executeCoreSkillCommand;

    public WelcomeChatData()
    {
    }

    public WelcomeChatData(CCodeExplainWindowControlViewModel cCodeExplainWindowControlViewModel)
    {
        Who = EWho.Welcome;
        Content = Resources.Resources.WhenDoubtAI;        

        Parent = cCodeExplainWindowControlViewModel;
        CoreSkill = Parent.SkillsProvider
            ?.GetSkills()
            ?.FirstOrDefault(p => p.Name == "CoreSkill");
        SemanticFunctions = CoreSkill?.SemanticFunctions
            .Select(p => (IQuickChatSkill)p)
            .Append(new GenerateCommitMsg())
            .Append(new GeneratePRMsg())
            .ToList();
    }

    public List<IQuickChatSkill> SemanticFunctions { get; set; }

    public CCodeExplainWindowControlViewModel Parent { get; }

    public SkillModel CoreSkill { get; }

    public AsyncRelayCommand<IQuickChatSkill> ExecuteCoreSkillCommand => _executeCoreSkillCommand ??= new AsyncRelayCommand<IQuickChatSkill>(ExcuteCodeSkillSKFunctionAsync);

    private async Task ExcuteCodeSkillSKFunctionAsync(
        IQuickChatSkill quickChatSkill,
        CancellationToken cancellationToken)
    {
        if (quickChatSkill == null || CoreSkill == null)
        {
            return;
        }
        Parent.AiLoading();
        try
        {
            if (quickChatSkill is LocalSemanticFunctionModel)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                //get document view
                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView == null) return;
                if (docView?.TextView == null) return; //not a text window

                //get selection
                var selection = docView?.TextView?.Selection;
                if (selection == null) return;

                SnapshotSpan selectedSpan = selection.StreamSelectionSpan.SnapshotSpan;
                string selectedText = selectedSpan.GetText();

                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    await VS.MessageBox.ShowWarningAsync("Please select some code first.");
                    return;
                }

                var extension = Path.GetExtension(docView.FilePath);

                var codeType = CodeExtension.GetCodeType(extension);

                var coreSkill = KernelFactory.SKernel.ImportSemanticSkillFromDirectory(Parent.SkillsProvider.SkillsLocation, CoreSkill.Name);

                await Parent.CodeSkillAsync(selectedText, codeType, coreSkill[quickChatSkill.Name]);
            }
            else if(quickChatSkill is GenerateCommitMsg) {
                await GenerateCommitMsgAsync();
            }else if(quickChatSkill is GeneratePRMsg) {
                await GeneratePRMsgAsync();
            }
        }
        catch (Exception ex)
        {
            await VS.MessageBox.ShowErrorAsync(ex.Message);
        }
        finally
        {
            Parent.CoreSkillcancellationTokenSource?.Dispose();
            Parent.CoreSkillcancellationTokenSource = null;
            Parent.AiLoading();
        }
    }

    private async Task GeneratePRMsgAsync()
    {
        var gitReposDir = await GetWorkDirectoryAsync();
        if (string.IsNullOrEmpty(gitReposDir))
        {
            await VS.MessageBox.ShowErrorAsync("Git Repostiory Not Found");
            return;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "show --ignore-space-change origin/main..HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = gitReposDir
            }
        };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();

        Parent.CoreSkillcancellationTokenSource = new CancellationTokenSource();
        var token = Parent.CoreSkillcancellationTokenSource.Token;

        var pullRequestSkill = KernelFactory.SKernel.ImportSkill(new PRSkill.PullRequestSkill(KernelFactory.SKernel));

        var variables = new ContextVariables();
        variables["input"] = output;
        variables["culture"] = System.Globalization.CultureInfo.CurrentCulture.EnglishName;

        var kernelResponse = await KernelFactory.SKernel.RunAsync(variables,token, pullRequestSkill["GeneratePR"]);

        if (kernelResponse.ErrorOccurred)
        {
            throw kernelResponse.LastException;
        }

        Parent.ChatDatas.Add(new ChatData()
        {
            Content = kernelResponse.ToString(),
            Who = EWho.User,
        });
    }

    private async Task GenerateCommitMsgAsync()
    {
        var gitReposDir = await GetWorkDirectoryAsync();
        if (string.IsNullOrEmpty(gitReposDir))
        {
            await VS.MessageBox.ShowErrorAsync("Git Repostiory Not Found");
            return;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --staged",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                WorkingDirectory = gitReposDir
            }
        };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();

        if (string.IsNullOrEmpty(output))
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff HEAD~1",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    WorkingDirectory = gitReposDir
                }
            };
            process.Start();

            output = await process.StandardOutput.ReadToEndAsync();
        }

        Parent.CoreSkillcancellationTokenSource = new CancellationTokenSource();
        var token =Parent.CoreSkillcancellationTokenSource.Token;

        var pullRequestSkill = KernelFactory.SKernel.ImportSkill(new PRSkill.PullRequestSkill(KernelFactory.SKernel));

        var variables = new ContextVariables();
        variables["input"] = output;
        variables["culture"] = System.Globalization.CultureInfo.CurrentCulture.EnglishName;

        var kernelResponse = await KernelFactory.SKernel.RunAsync(
            variables,
            token,
            pullRequestSkill["GenerateCommitMessage"]);

        if (kernelResponse.ErrorOccurred)
        {
            throw kernelResponse.LastException;
        }

        Parent.ChatDatas.Add(new ChatData()
        {
            Content = kernelResponse.ToString(),
            Who = EWho.User,
        });
    }

    private async Task<string> GetWorkDirectoryAsync()
    {
        var sln = await VS.Solutions?.GetCurrentSolutionAsync();

        if (sln == null)
        {
            VS.MessageBox.ShowWarning("Please open a slution");
            return null;
        }

        var vsItemDir = new DirectoryInfo(Path.GetDirectoryName(sln.FullPath));
        var root = vsItemDir.Root;

        while (!vsItemDir.GetDirectories().Any(p => p.Name.EndsWith(".git")))
        {
            vsItemDir = vsItemDir.Parent;
            if (vsItemDir == root)
            {
                break;
            }
        }

        if (vsItemDir.GetDirectories().Any(p => p.Name.EndsWith(".git")))
        {
            return vsItemDir.FullName;
        }

        var projects = await VS.Solutions.GetAllProjectsAsync(ProjectStateFilter.Loaded);
        var project = projects.FirstOrDefault();
        if (project == null)
        {
            return null;
        }

        vsItemDir = new DirectoryInfo(Path.GetDirectoryName(project.FullPath));
        root = vsItemDir.Root;

        while (!vsItemDir.GetDirectories().Any(p => p.Name.EndsWith(".git")))
        {
            vsItemDir = vsItemDir.Parent;
            if (vsItemDir == root)
            {
                break;
            }
        }

        if (vsItemDir.GetDirectories().Any(p => p.Name.EndsWith(".git")))
        {
            return vsItemDir.FullName;
        }

        return null;
    }
}
