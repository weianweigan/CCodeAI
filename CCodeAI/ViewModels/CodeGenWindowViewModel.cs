﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Documents;

namespace CCodeAI.ViewModels;

public partial class CodeGenWindowViewModel : ObservableObject
{
    private IAsyncRelayCommand _sendCommand;
    private string _output;

    public CodeGenWindowViewModel(string language)
    {
        KernelFactory.Init();

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CCodeAISkills");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var defaultDir = Path.Combine(
            Path.GetDirectoryName(typeof(CodeGenWindowViewModel).Assembly.Location),
            "Skills");

        SkillsProvider = new SkillsProvider(dir, defaultDir);

        SemanticFunctions = new ListCollectionView(
            SkillsProvider.GetSkills().SelectMany(p => p.SemanticFunctions).ToList());
        SemanticFunctions.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        Language = language;
    }

    public IKernel SKernel => KernelFactory.SKernel;

    public ISkillsProvider SkillsProvider { get; }

    public ListCollectionView SemanticFunctions { get; set; }

    public LocalSemanticFunctionModel SelectedSemanticModel { get; set; }

    public string Input { get; set; }

    public string Output { get => _output; set => SetProperty(ref _output , value); }

    public IAsyncRelayCommand SendCommand { get => _sendCommand ??= new AsyncRelayCommand(SendAsync); }

    public Action CloseAction { get; internal set; }

    public Action<bool?> DialogResult { get; internal set; }

    public string Language { get; }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (SelectedSemanticModel == null || string.IsNullOrWhiteSpace(Input))
        {
            return;
        }

        var skill = SKernel.ImportSemanticSkillFromDirectory(SelectedSemanticModel.RootDir, SelectedSemanticModel.Category);

        var variables = new ContextVariables();
        variables["input"] = Input;
        variables["language"] = Language;
        variables["culture"] = System.Globalization.CultureInfo.CurrentCulture.EnglishName;

        var result = await SKernel.RunAsync(
            variables,
            cancellationToken,
            skill[SelectedSemanticModel.Name]);

        if (result.ErrorOccurred)
        {
            await VS.MessageBox.ShowErrorAsync(result.LastErrorDescription);
        }

        Output = result.ToString();
    }

    [RelayCommand]
    private void Stop()
    {
        if (SendCommand.IsRunning)
        {
            SendCommand.Cancel();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult.Invoke(false);
    }

    [RelayCommand]
    private void Insert()
    {
        DialogResult?.Invoke(true);
    }
}
