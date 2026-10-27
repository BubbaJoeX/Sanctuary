using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using Sanctuary.Core.Configuration;

namespace Sanctuary.Game.Conversations;

/// <summary>
/// The two Studio Umbra API v1 calls the NPC chat bot needs. A conversation keeps its history on the server,
/// so each message only carries the new line.
/// </summary>
public sealed class StudioUmbraChatClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly HttpClient _httpClient;
    private readonly NpcChatOptions _options;

    public StudioUmbraChatClient(NpcChatOptions options)
    {
        _options = options;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = Timeout
        };

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    public async Task<string> StartConversationAsync(string title, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("conversations", new { title }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreatedConversation>(cancellationToken);

        return created?.Id ?? throw new HttpRequestException("Studio Umbra returned no conversation id.");
    }

    /// <summary>
    /// Sends a message and returns the reply, or null if the bot didn't finish one.
    /// </summary>
    public async Task<string?> SendAsync(string conversationId, string content, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            content,
            modelId = string.IsNullOrWhiteSpace(_options.ModelId) ? null : _options.ModelId
        };

        using var response = await _httpClient.PostAsJsonAsync($"conversations/{conversationId}/messages", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var reply = await response.Content.ReadFromJsonAsync<MessageReply>(cancellationToken);

        return reply?.Status is "completed" or "truncated" ? reply.Reply : null;
    }

    private sealed record CreatedConversation(string Id);

    private sealed record MessageReply(string? Status, string? Reply);
}
