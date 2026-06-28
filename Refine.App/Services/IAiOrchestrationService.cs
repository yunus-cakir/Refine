using Refine.App.Models.Entities;

namespace Refine.App.Services;

public interface IAiOrchestrationService
{
    Task<string> GenerateResponseAsync(ChatSession session, string userMessage);
    Task<string> GenerateTitleAsync(string firstMessage);
    void SetApiKey(string apiKey);
    bool IsConfigured();
}
