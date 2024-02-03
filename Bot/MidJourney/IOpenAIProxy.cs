using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;

namespace Bot;

public interface IOpenAIProxy
{
    Task<ChatCompletionMessage[]> SendChatMessage(string message);
}