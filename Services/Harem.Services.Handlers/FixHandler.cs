using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class FixHandler : ICommandHandler
{
    public const string CommandName = "fix";
    public string Command => CommandName;

    public Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        return Task.FromResult(new CommandResult
        {
            CommandReply = "摘要相关功能已移至 `/summary` 命令。可用：`/summary update` 增量摘要，`/summary force` 强制重生成。"
        });
    }
}
