using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

class Program
{
    static async Task Main()
    {
        var apiKey = dotnet user-secrets list --project src/SmartHadithTree.Api | Select-String -Pattern 'Gemini:ApiKey = (.+)' | % { $_.Matches.Groups[1].Value };
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", apiKey);
        var kernel = builder.Build();
        
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        try {
            var response = await chat.GetChatMessageContentAsync("Hello");
            Console.WriteLine("Success: " + response.Content);
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
