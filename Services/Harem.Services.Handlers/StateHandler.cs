using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class StateHandler : ICommandHandler
{
    public const string CommandName = "state";
    public string Command => CommandName;

    private readonly IStateService _stateService;

    public StateHandler(IStateService stateService)
    {
        _stateService = stateService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 2);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "get" : parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : "";

        return subCmd switch
        {
            "set" => await HandleSetAsync(subArgs),
            "get" => await HandleGetAsync(subArgs),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`" }
        };
    }

    private async Task<CommandResult> HandleGetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            var states = await _stateService.GetAllStatesAsync();
            if (states.Count == 0)
                return new CommandResult { CommandReply = "当前没有状态记录。" };

            var lines = new List<string> { "**📊 角色状态**", "" };
            lines.Add("| 状态 | 值 | 说明 |");
            lines.Add("|------|------|------|");
            foreach (var s in states)
                lines.Add($"| {s.Name} | {s.Value} | {s.Description} |");

            return new CommandResult { CommandReply = string.Join("\n", lines) };
        }

        var state = await _stateService.GetStateAsync(key);
        if (state == null)
            return new CommandResult { CommandReply = $"状态 `{key}` 不存在。" };

        return new CommandResult
        {
            CommandReply = $"**{state.Name}**: {state.Value}\n> {state.Description}"
        };
    }

    private async Task<CommandResult> HandleSetAsync(string args)
    {
        var kv = args.Split(' ', 2);
        if (kv.Length < 2)
            return new CommandResult { CommandReply = "格式错误。用法: `/state set 键 值`" };

        var key = kv[0];
        var value = kv[1];

        var success = await _stateService.UpdateStateAsync(key, value);
        return success
            ? new CommandResult { CommandReply = $"✅ 已更新 `{key}` = `{value}`" }
            : new CommandResult { CommandReply = $"状态 `{key}` 不存在，无法修改。" };
    }
}