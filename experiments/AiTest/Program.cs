using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

class Program
{
    static async Task Main()
    {
        var builder = Kernel.CreateBuilder();
        
        // Use Ollama through its OpenAI-compatible endpoint
        builder.AddOpenAIChatCompletion(
            modelId: "llama3.2:3b",
            apiKey: "EMPTY", 
            endpoint: new Uri("http://localhost:11434/v1")
        );
        
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
