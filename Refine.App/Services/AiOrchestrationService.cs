using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Refine.App.Models.Entities;
using Microsoft.Maui.Storage;

namespace Refine.App.Services;

public class AiOrchestrationService : IAiOrchestrationService
{
    private readonly HttpClient _httpClient;
    private readonly LocalDbService _dbService;
    private string _apiKey = string.Empty;
    private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string ModelId = "llama-3.3-70b-versatile";

    public AiOrchestrationService(LocalDbService dbService)
    {
        _httpClient = new HttpClient();
        _dbService = dbService;
        
        // Try to load API key from Preferences
        _apiKey = Preferences.Default.Get("GroqApiKey", string.Empty);
    }

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
        Preferences.Default.Set("GroqApiKey", apiKey);
    }

    public bool IsConfigured() => !string.IsNullOrEmpty(_apiKey);

    public async Task<string> GenerateResponseAsync(ChatSession session, string userMessage)
    {
        if (!IsConfigured())
        {
            return "Please configure your Groq API Key in the settings first.";
        }

        // Save user message
        var userMsg = new ChatMessage
        {
            ChatSessionId = session.Id,
            Role = "user",
            Content = userMessage
        };
        await _dbService.SaveChatMessageAsync(userMsg);
        session.Messages.Add(userMsg);

        // Build prompt context
        var history = session.Messages.OrderBy(m => m.CreatedAt).TakeLast(10).ToList();

        var messages = new List<object>();
        messages.Add(new { role = "system", content = "You are an elite, highly knowledgeable fitness coach named Refine AI. Your goal is to help the user achieve their fitness goals by providing accurate, science-based, and personalized advice. Keep responses concise and formatted in markdown." });
        
        foreach (var msg in history)
        {
            messages.Add(new
            {
                role = msg.Role == "model" ? "assistant" : "user",
                content = msg.Content
            });
        }

        var requestBody = new
        {
            model = ModelId,
            messages = messages
        };

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
            var response = await _httpClient.PostAsJsonAsync(ApiUrl, requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var errorMsg = $"Error connecting to Groq AI: {(int)response.StatusCode} {response.ReasonPhrase}\nDetails: {errorBody}";
                
                var errorModelMsg = new ChatMessage
                {
                    ChatSessionId = session.Id,
                    Role = "model",
                    Content = errorMsg
                };
                await _dbService.SaveChatMessageAsync(errorModelMsg);
                session.Messages.Add(errorModelMsg);
                
                return errorMsg;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;
            
            var generatedText = root.GetProperty("choices")[0]
                                    .GetProperty("message")
                                    .GetProperty("content").GetString() ?? "Sorry, I couldn't generate a response.";

            var modelMsg = new ChatMessage
            {
                ChatSessionId = session.Id,
                Role = "model",
                Content = generatedText
            };
            await _dbService.SaveChatMessageAsync(modelMsg);
            session.Messages.Add(modelMsg);

            return generatedText;
        }
        catch (Exception ex)
        {
            var exMsg = $"Error connecting to Groq AI: {ex.Message}";
            var modelMsg = new ChatMessage
            {
                ChatSessionId = session.Id,
                Role = "model",
                Content = exMsg
            };
            await _dbService.SaveChatMessageAsync(modelMsg);
            session.Messages.Add(modelMsg);
            return exMsg;
        }
    }

    public async Task<string> GenerateTitleAsync(string firstMessage)
    {
        if (!IsConfigured()) return "New Chat";

        var requestBody = new
        {
            model = ModelId,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant. Generate a very short, concise title (maximum 3-4 words) that summarizes the following message. Respond ONLY with the title text, nothing else." },
                new { role = "user", content = firstMessage }
            }
        };

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
            var response = await _httpClient.PostAsJsonAsync(ApiUrl, requestBody);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;
                
                var title = root.GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content").GetString()?.Trim('"', '\'', ' ', '\n', '\r');

                return string.IsNullOrWhiteSpace(title) ? "New Chat" : title;
            }
        }
        catch
        {
            // Ignore errors for title generation
        }

        return "New Chat";
    }
}
