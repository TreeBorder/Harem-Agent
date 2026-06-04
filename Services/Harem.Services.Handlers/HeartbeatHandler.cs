using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class HeartbeatHandler : ICommandHandler
{
    public const string CommandName = "heartbeat";
    public string Command => CommandName;

    private readonly IHeartbeatService _heartbeatService;

    public HeartbeatHandler(IHeartbeatService heartbeatService)
    {
        _heartbeatService = heartbeatService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        await _heartbeatService.ExecuteGreetingAsync();
        return new CommandResult { CommandReply = "💓 心跳已手动触发" };
    }
}
