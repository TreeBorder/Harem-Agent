using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Harem.Cli.Commands;

[Description("会话管理 — 列出、重置会话")]
public sealed class SessionCommand : Command<SessionCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        // 目前只输出帮助，以后扩展
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        AnsiConsole.MarkupLine("[yellow]用法:[/] harem-cli session <command>");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("  [green]list[/]  列出所有会话");
        AnsiConsole.MarkupLine("  [green]reset[/] 重置指定会话");
        return 0;
    }
}
