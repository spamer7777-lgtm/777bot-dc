using OpenAI;
using OpenAI.Chat;
using System.Threading.Tasks;

public static class ChatGPTService
{
    private static readonly OpenAIClient client = new OpenAIClient("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");

    public static async Task<string> AskAsync(string prompt)
    {
        var chatRequest = new ChatCompletionsOptions()
        {
            Messages = 
            {
                new ChatMessage(ChatRole.User, prompt)
            },
            MaxTokens = 300
        };

        var response = await client.ChatCompletions.CreateAsync("gpt-3.5-turbo", chatRequest);
        return response.Choices[0].Message.Content.Trim();
    }
}
