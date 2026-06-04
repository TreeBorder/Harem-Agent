using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;
using Microsoft.Extensions.Options;

namespace Harem.Services.Handlers;

public class ModelHandler : ICommandHandler
{
    public const string CommandName = "model";
    public string Command => CommandName;

    private readonly AgentOptions _agentOptions;
    private readonly ISessionService _sessionService;

    public ModelHandler(IOptions<AgentOptions> agentOptions, ISessionService sessionService)
    {
        _agentOptions = agentOptions.Value;
        _sessionService = sessionService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 2);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "list" : parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : "";

        var result = subCmd switch
        {
            "list" => await HandleListAsync(sessionKey),
            "switch" => HandleSwitch(subArgs),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`。用法: `/model [list|switch]`" }
        };

        return result;
    }

    private async Task<CommandResult> HandleListAsync(string sessionKey)
    {
        var lines = new List<string> { "**🤖 模型列表**", "" };

        // 从会话读取当前实际运行模型（可能因回退而与默认不同）
        var session = await _sessionService.GetSessionAsync(sessionKey);
        var currentModel = string.IsNullOrEmpty(session.Info.ModelKey)
            ? _agentOptions.DefaultModelKey
            : session.Info.ModelKey;
        lines.Add($"- **{currentModel}** (当前运行)");

        // 默认模型（如果与当前不同则补充显示）
        if (currentModel != _agentOptions.DefaultModelKey)
        {
            lines.Add($"- {_agentOptions.DefaultModelKey} (默认)");
        }

        // 回退模型
        foreach (var fallback in _agentOptions.FallbackModels)
        {
            lines.Add($"- {fallback} (回退)");
        }

        // 所有可用模型（从 ModelProviders 展开）
        lines.Add("");
        lines.Add("**所有已配置模型:**");
        foreach (var provider in _agentOptions.ModelProviders)
        {
            foreach (var model in provider.Models)
            {
                var key = $"{provider.Name}/{model.Name}";
                var isDefault = key == _agentOptions.DefaultModelKey;
                var isFallback = _agentOptions.FallbackModels.Contains(key);
                var tag = isDefault ? " ← 默认" : isFallback ? " ← 回退" : "";
                var desc = !string.IsNullOrEmpty(model.Description) ? $" — {model.Description}" : "";
                lines.Add($"- `{key}`{desc}{tag}");
            }
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    private CommandResult HandleSwitch(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
            return new CommandResult { CommandReply = "请指定模型。用法: `/model switch Provider/Model`" };

        // 验证模型是否存在
        var parts = modelKey.Trim().Split('/', 2);
        if (parts.Length != 2)
            return new CommandResult { CommandReply = $"模型格式错误: `{modelKey}`，应为 `Provider/Model`" };

        var providerName = parts[0];
        var modelName = parts[1];
        var provider = _agentOptions.ModelProviders.FirstOrDefault(p => p.Name == providerName);
        if (provider == null)
            return new CommandResult { CommandReply = $"未找到模型提供者: `{providerName}`" };

        var model = provider.Models.FirstOrDefault(m => m.Name == modelName);
        if (model == null)
            return new CommandResult
            {
                CommandReply = $"未找到模型: `{modelName}`（提供者: `{providerName}`）\n可用模型: {string.Join(", ", provider.Models.Select(m => m.Name))}"
            };

        return new CommandResult
        {
            CommandReply = $"✅ 模型已切换到 `{modelKey}`",
            SwitchModelKey = modelKey.Trim()
        };
    }
}
