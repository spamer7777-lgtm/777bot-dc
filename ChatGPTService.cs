using OpenAI;
using OpenAI.Chat;
using System.Threading.Tasks;

public static class ChatGPTService
{
    private static readonly OpenAIClient client = new OpenAIClient("YOUR_OPENAI_API_KEY");

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
