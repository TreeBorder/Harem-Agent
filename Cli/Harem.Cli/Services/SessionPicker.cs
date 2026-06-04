using Microsoft.AspNetCore.SignalR.Client;
using Spectre.Console;

namespace Harem.Cli.Services;

/// <summary>
/// SignalR 连接与会话管理（纯 HTTP + SignalR，不依赖 Harem 内部服务）
/// </summary>
public static class SessionPicker
{
    /// <summary>连接 SignalR Hub</summary>
    public static async Task<HubConnection> ConnectAsync(string hubUrl, CancellationToken ct)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("正在连接 Harem…", async _ =>
            {
                try { await connection.StartAsync(ct); }
                catch (Exception ex) { throw new Exception($"连接失败: {ex.Message}"); }
            });

        return connection;
    }

    /// <summary>交互式选择 sessionKey（从 API 拉取）</summary>
    public static async Task<string> PickSessionKeyAsync()
    {
        var sessions = await CliSettings.GetAsync<List<SessionDto>>("/sessions");
        if (sessions is { Count: > 0 })
        {
            var choices = sessions.Select(s =>
            {
                var time = DateTime.FromFileTimeUtc(s.UpdateAt).ToLocalTime();
                return $"{s.Key}  [grey]({s.SessionId[..8]}… {time:MM-dd HH:mm})[/]";
            }).ToList();

            choices.Add("[grey]signalr:direct:user — 新建 SignalR 会话[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]选择会话[/]")
                    .PageSize(10)
                    .MoreChoicesText("[grey](上下翻动查看更多)[/]")
                    .AddChoices(choices));

            return selected.Split("  [grey]")[0].Trim();
        }

        AnsiConsole.MarkupLine("[grey]无现有会话，使用默认: signalr:direct:user[/]");
        return "signalr:direct:user";
    }

    /// <summary>从 API 拉取历史消息</summary>
    public static async Task<List<HistoryMsg>> GetHistoryAsync(string sessionKey, int limit = 30)
    {
        var encoded = Uri.EscapeDataString(sessionKey);
        var result = await CliSettings.GetAsync<HistoryResponse>($"/chat/history?sessionKey={encoded}&limit={limit}");
        return result?.Messages ?? [];
    }

    public record SessionDto(string Key, string SessionId, long UpdateAt);
    public record HistoryMsg(string Role, string Text, long Time);
    public record HistoryResponse(string SessionKey, string SessionId, List<HistoryMsg> Messages);
}
