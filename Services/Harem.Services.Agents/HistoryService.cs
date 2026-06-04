using System.Text.RegularExpressions;
using Harem.Contracts.Configurations;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

public class HistoryService(
    IOptions<RuntimeOptions> options,
    IFileService fileService) : IHistoryService
{
    private readonly string _workspaceDirectory = options.Value.Workspace;
    private const string HistoryDirectory = ".history";
    private const string HistoryFile = "history.json";

    public async Task<ChatHistory> GetHistoryAsync(string sessionId)
    {
        var fileName = Path.Combine(_workspaceDirectory, HistoryDirectory, sessionId, HistoryFile);
        var history = await fileService.ReadJsonAsync<ChatHistory>(fileName);
        return history ?? new ChatHistory
        {
            Messages = [],
        };
    }

    public async Task SaveHistoryAsync(string sessionId, ChatHistory history)
    {
        var fileName = Path.Combine(_workspaceDirectory, HistoryDirectory, sessionId, HistoryFile);
        await fileService.WriteJsonAsync(fileName, history);
    }

    /// <summary>
    /// 保存本轮交互到历史记录（不含摘要，摘要由 SummaryService 独立管理）
    /// </summary>
    public async Task SaveHistoryAsync(string sessionId, ChatHistory history, ChatMessage inputMessage, string content)
    {
        var userMessage = inputMessage.Text;
        var timeTag = Regex.Match(userMessage, @"<Time>(.*?)</Time>", RegexOptions.Singleline).Groups[0].Value;
        var messageTag = Regex.Match(userMessage, @"<UserMessage>(.*?)</UserMessage>", RegexOptions.Singleline)
            .Groups[0].Value;
        var inputText = $"{timeTag}{messageTag}";
        if (!string.IsNullOrWhiteSpace(inputText))
            history.Messages.Add(new HistoryMessage
            {
                Role = HistoryRole.User,
                Text = inputText,
                CreatedAt = inputMessage.CreatedAt ?? DateTimeOffset.UtcNow
            });

        history.Messages.Add(new HistoryMessage
        {
            Role = HistoryRole.Assistant,
            Text = content,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await SaveHistoryAsync(sessionId, history);
    }

    public async Task SetHistoryBag<T>(string sessionId, string key, T content)
    {
        var fileName = Path.Combine(_workspaceDirectory, HistoryDirectory, sessionId, $"{key}.json");
        await fileService.WriteJsonAsync(fileName, content);
    }

    public async Task<T?> GetHistoryBag<T>(string sessionId, string key)
    {
        var fileName = Path.Combine(_workspaceDirectory, HistoryDirectory, sessionId, $"{key}.json");
        return await fileService.ReadJsonAsync<T>(fileName);
    }
}
