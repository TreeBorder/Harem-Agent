using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Harem.Cli.Commands;

[Description("工作区管理 — 创建、校验、修复工作区")]
public sealed class WorkspaceCommand : AsyncCommand<WorkspaceCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("工作区路径")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        await Task.CompletedTask;
        AnsiConsole.MarkupLine("[yellow]用法:[/] harem-cli workspace <command> [options]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("  [green]init[/]     创建工作区模板");
        AnsiConsole.MarkupLine("  [green]validate[/] 校验工作区完整性");
        AnsiConsole.MarkupLine("  [green]repair[/]   修复损坏的工作区");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine($"当前路径: [grey]{settings.Path ?? "(未指定)"}[/]");
        return 0;
    }
}

[Description("创建工作区模板")]
public sealed class WorkspaceInitCommand : AsyncCommand<WorkspaceCommand.Settings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, WorkspaceCommand.Settings settings, CancellationToken cancellation)
    {
        await Task.CompletedTask;
        var path = settings.Path ?? Environment.GetEnvironmentVariable("HAREM_WORKSPACE");
        if (string.IsNullOrWhiteSpace(path))
        {
            AnsiConsole.MarkupLine("[red]❌ 请指定工作区路径[/]");
            return 1;
        }

        AnsiConsole.Status().Start("正在创建工作区…", _ =>
        {
            Directory.CreateDirectory(Path.Combine(path, "memories"));
            Directory.CreateDirectory(Path.Combine(path, ".history"));
            Directory.CreateDirectory(Path.Combine(path, ".sessions"));
            Directory.CreateDirectory(Path.Combine(path, ".npc"));
            Directory.CreateDirectory(Path.Combine(path, ".heartbeat"));
            Directory.CreateDirectory(Path.Combine(path, ".cron"));
            Directory.CreateDirectory(Path.Combine(path, ".vector"));
            Directory.CreateDirectory(Path.Combine(path, ".game"));
            Directory.CreateDirectory(Path.Combine(path, ".game/engines"));
            Directory.CreateDirectory(Path.Combine(path, ".game/levels"));
        });

        AnsiConsole.MarkupLine($"[green]✅ 工作区已创建: {path.EscapeMarkup()}[/]");
        return 0;
    }
}

[Description("校验工作区完整性")]
public sealed class WorkspaceValidateCommand : AsyncCommand<WorkspaceCommand.Settings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, WorkspaceCommand.Settings settings, CancellationToken cancellation)
    {
        await Task.CompletedTask;
        var path = settings.Path ?? Environment.GetEnvironmentVariable("HAREM_WORKSPACE");
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            AnsiConsole.MarkupLine("[red]❌ 工作区不存在[/]");
            return 1;
        }

        var required = new[] { "AGENTS.md", "SOUL.md", "USER.md", "config.json" };
        var allGood = true;

        foreach (var file in required)
        {
            var exists = File.Exists(Path.Combine(path, file));
            var icon = exists ? "[green]✅[/]" : "[red]❌[/]";
            AnsiConsole.MarkupLine($"  {icon} {file}");
            if (!exists) allGood = false;
        }

        return allGood ? 0 : 1;
    }
}
