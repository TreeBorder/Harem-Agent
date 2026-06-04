using System.Text.Json;
using Harem.Contracts.Configurations;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

public class SessionService(
    IOptions<RuntimeOptions> options,
    IFileService fileService)
    : ISessionService
{
    private readonly string _workspaceDirectory = options.Value.Workspace;
    private const string SessionDirectory = ".sessions";
    private const string SessionConfigurationFile = "config.json";

    private async Task<List<SessionInfo>> LoadSessionsAsync()
    {
        var configFile = Path.Combine(_workspaceDirectory, SessionDirectory, SessionConfigurationFile);
        var sessions = await fileService.ReadJsonAsync<List<SessionInfo>>(configFile);
        return sessions ?? [];
    }

    public SessionInfo CreateNewSessionAsync(string key)
    {
        return new SessionInfo
        {
            Key = key,
            SessionId = Guid.NewGuid().ToString(),
            UpdateAt = DateTime.UtcNow.ToFileTimeUtc()
        };
    }

    public SessionInfo CreateNewSessionAsync(string channel, string type, string topic)
    {
        return CreateNewSessionAsync($"{channel}:{type}:{topic}");
    }

    public async Task<bool> UpdateSessionAsync(Session session)
    {
        session.Info.UpdateAt = DateTime.UtcNow.ToFileTimeUtc();

        var sessions = await LoadSessionsAsync();
        var index = sessions.FindIndex(s => s.Key == session.Info.Key);
        if (index == -1) sessions.Add(session.Info);
        else sessions[index] = session.Info;

        // SystemSent 唯一性：开启当前则关闭其他
        if (session.Info.SystemSent)
        {
            foreach (var s in sessions)
            {
                if (s.Key != session.Info.Key)
                    s.SystemSent = false;
            }
        }

        //保存会话配置
        var configFile = Path.Combine(_workspaceDirectory, SessionDirectory, SessionConfigurationFile);
        await fileService.WriteJsonAsync(configFile, sessions);

        //保存会话内容
        var sessionFile = Path.Combine(_workspaceDirectory, SessionDirectory, session.Info.SessionId);
        await fileService.WriteJsonAsync(sessionFile, session.Content);
        return true;
    }

    public async Task<Session> GetSessionAsync(string channel, string type, string topic)
    {
        var info = await GetSessionInfoAsync(channel, type, topic);
        var sessionFile = Path.Combine(_workspaceDirectory, SessionDirectory, info.SessionId);
        var content = await fileService.GetFileAsync(sessionFile);
        return new Session
        {
            Info = info,
            Content = string.IsNullOrWhiteSpace(content) ? default : JsonElement.Parse(content)
        };
    }

    public async Task<Session> GetSessionAsync(string key)
    {
        var info = await GetSessionInfoAsync(key) ?? CreateNewSessionAsync(key);
        var sessionFile = Path.Combine(_workspaceDirectory, SessionDirectory, info.SessionId);
        var content = await fileService.GetFileAsync(sessionFile);
        return new Session
        {
            Info = info,
            Content = string.IsNullOrWhiteSpace(content) ? default : JsonElement.Parse(content)
        };
    }

    public async Task<SessionInfo> GetSessionInfoAsync(string channel, string type, string topic)
    {
        var sessions = await LoadSessionsAsync();
        var info = sessions.FirstOrDefault(s => s.Key == $"{channel}:{type}:{topic}") ??
                   CreateNewSessionAsync(channel, type, topic);
        return info;
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(string key)
    {
        var sessions = await LoadSessionsAsync();
        var info = sessions.FirstOrDefault(s => s.Key == key);
        return info;
    }

    public async Task<IReadOnlyList<SessionInfo>> GetAllSessionsAsync()
    {
        return await LoadSessionsAsync();
    }

    public async Task<SessionInfo?> GetSystemSentSessionAsync()
    {
        var sessions = await LoadSessionsAsync();
        return sessions.FirstOrDefault(s => s.SystemSent);
    }

    public async Task<Session> ResetSessionAsync(string key)
    {
        var sessions = await LoadSessionsAsync();
        var index = sessions.FindIndex(s => s.Key == key);

        SessionInfo info;
        if (index >= 0)
        {
            // 保留元信息，生成新 SessionId
            info = sessions[index];
            info.SessionId = sessions[index].SessionId;
            info.UpdateAt = DateTime.UtcNow.ToFileTimeUtc();
            info.InputTokens = 0;
            info.OutputTokens = 0;
        }
        else
        {
            info = CreateNewSessionAsync(key);
        }

        // 保存会话配置
        var configFile = Path.Combine(_workspaceDirectory, SessionDirectory, SessionConfigurationFile);
        await fileService.WriteJsonAsync(configFile, sessions);

        return new Session { Info = info, Content = default };
    }
}