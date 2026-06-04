using Harem.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("harem-cli");
    config.SetApplicationVersion("0.1.0");

    // 命令注册
    config.AddCommand<ChatCommand>("chat")
        .WithDescription("交互式聊天");

    config.AddCommand<SessionCommand>("session")
        .WithDescription("会话管理");

    config.AddBranch("workspace", workspace =>
    {
        workspace.SetDescription("工作区管理");
        workspace.AddCommand<WorkspaceInitCommand>("init")
            .WithDescription("创建工作区模板");
        workspace.AddCommand<WorkspaceValidateCommand>("validate")
            .WithDescription("校验工作区完整性");
    });
});

try
{
    return await app.RunAsync(args);
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}
