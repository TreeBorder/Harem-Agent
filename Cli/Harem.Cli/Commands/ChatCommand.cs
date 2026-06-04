using Harem.Cli.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Harem.Cli.Commands;

[Description("交互式聊天 — 连上 Harem 开始对话")]
public sealed class ChatCommand : AsyncCommand<ChatCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Harem 服务地址")]
        [CommandOption("-s|--server")]
        public string? Server { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        CliSettings.Initialize(settings.Server, null);

        try
        {
            // 1. 连接 Hub
            var connection = await SessionPicker.ConnectAsync(CliSettings.HubUrl, cancellation);
            AnsiConsole.MarkupLine("[green]✅ 已连接到 Harem[/]");

            // 2. 选 session
            var sessionKey = await SessionPicker.PickSessionKeyAsync();
            AnsiConsole.MarkupLine($"[grey]会话: {sessionKey.EscapeMarkup()}[/]");

            // 3. 拉取历史消息
            var history = await SessionPicker.GetHistoryAsync(sessionKey);
            if (history.Count > 0)
            {
                AnsiConsole.MarkupLine($"[grey]历史消息 ({history.Count} 条):[/]\n");
                foreach (var msg in history)
                {
                    var label = msg.Role == "User" ? "[bold yellow]你[/]" : "[bold magenta]小骚货[/]";
                    var time = DateTimeOffset.FromUnixTimeMilliseconds(msg.Time).ToLocalTime().ToString("HH:mm");
                    AnsiConsole.MarkupLine($"[grey]{time}[/] {label}");
                    MarkdownRenderer.Render(msg.Text);
                    AnsiConsole.MarkupLine("");
                }
            }

            // 4. 注册消息接收
            var replySemaphore = new SemaphoreSlim(0, 1);
            var isStreaming = false;

            connection.On<string>("ReceiveStream", message =>
            {
                if (!isStreaming)
                {
                    AnsiConsole.MarkupLine("\n[italic grey]> 正在输入…[/]");
                    isStreaming = true;
                }
                AnsiConsole.Cursor.MoveUp(1);
                AnsiConsole.MarkupLine($"[italic grey]{message.EscapeMarkup()}[/]");
            });

            connection.On<string>("ReceiveMessage", message =>
            {
                AnsiConsole.Cursor.MoveUp(1);
                MarkdownRenderer.Render(message);
                isStreaming = false;
                replySemaphore.Release();
            });

            // 5. 聊天循环
            AnsiConsole.MarkupLine("\n[grey]输入消息开始聊天，输入 /exit 退出，/new 重置会话[/]\n");

            while (true)
            {
                var input = AnsiConsole.Ask<string>("[bold yellow]你[/]");
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input == "/exit")
                    break;

                if (input == "/new")
                {
                    await connection.InvokeAsync("SendMessage", "/new", sessionKey, cancellation);
                    AnsiConsole.MarkupLine("[grey]会话已重置[/]");
                    continue;
                }

                await connection.InvokeAsync("SendMessage", input, sessionKey, cancellation);
                await replySemaphore.WaitAsync(cancellation);
            }

            await connection.DisposeAsync();
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ 错误: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
    }
}
