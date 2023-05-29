using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using System.Threading;
using System.Threading.Tasks;

public class KernelFactory
{
    public static bool UseAzureOpenAI { get; } = true;

    public static IKernel SKernel { get; private set; }

    public static void Init()
    {
        if (SKernel != null)
        {
            return;
        }

        SKernel = Kernel.Builder.Configure(c =>
        {
            if (UseAzureOpenAI)
            {
                //c.AddAzureTextCompletionService(
                //    "ccode",
                //    "text-davinci-003",
                //    AzureConfig.Endpoint,
                //    AzureConfig.AppKey
                //    );
                c.AddAzureChatCompletionService(
                    "gpt-35-turbo",
                    AzureConfig.Endpoint,
                    AzureConfig.AppKey,
                    true);
                c.AddAzureTextEmbeddingGenerationService
                (
                    "text-embedding-ada-002",
                    AzureConfig.Endpoint,
                    AzureConfig.AppKey,
                    "ada"
                );
                c.SetDefaultTextCompletionService("ccode");
            }
            else
            {
                //c.AddOpenAITextCompletionService(
                //    "ccode",
                //    OpenAIConfig.Model,
                //    OpenAIConfig.OpenAIKey
                //    );
            }
        })
        .WithMemoryStorage(new VolatileMemoryStore())
        .Build();
    }

    public static async Task<string> InvokeCodeFunctionAsync(
        string semanticFunction,
        string code,
        CancellationToken cancellationToken,
        string extension = "csharp")
    {
        Init();

        var explainFunc = SKernel.CreateSemanticFunction(semanticFunction);

        var context = SKernel.CreateNewContext();
        context.Variables["extension"] = extension;

        var result = await explainFunc.InvokeAsync(code, context,cancellationToken:cancellationToken);

        if (result.ErrorOccurred)
        {
            throw result.LastException;
        }

        return result.ToString().Trim();
    }
}
