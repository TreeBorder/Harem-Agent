using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class NpcHandler : ICommandHandler
{
    public const string CommandName = "npc";
    public string Command => CommandName;

    private readonly INpcService _npcService;

    public NpcHandler(INpcService npcService)
    {
        _npcService = npcService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 2);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "list" : parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : "";

        return subCmd switch
        {
            "list" => await HandleListAsync(),
            "get" => await HandleGetAsync(subArgs),
            "search" => await HandleSearchAsync(subArgs),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`。用法: `/npc [list|get|search]`" }
        };
    }

    private async Task<CommandResult> HandleListAsync()
    {
        var npcs = await _npcService.GetAllNpcsAsync();
        if (npcs.Count == 0)
            return new CommandResult { CommandReply = "当前没有 NPC 记录。" };

        var lines = new List<string> { "**👥 NPC 列表**", "" };
        lines.Add("| 名称 | 标签 | 亲密度 | 记忆深度 |");
        lines.Add("|------|------|--------|---------|");
        foreach (var npc in npcs)
        {
            var depth = npc.GetMemoryDepth();
            var tags = npc.Tags.Count > 0 ? string.Join(", ", npc.Tags) : "-";
            lines.Add($"| {npc.Name} | {tags} | {npc.CurrentIntimacy():F1} | {depth} |");
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    private async Task<CommandResult> HandleGetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new CommandResult { CommandReply = "请指定 NPC 的 key。用法: `/npc get 关键字`" };

        var npc = await _npcService.GetNpcAsync(key);
        if (npc == null)
            return new CommandResult { CommandReply = $"NPC `{key}` 不存在。" };

        var lines = new List<string>
        {
            $"**{npc.Name}** (`{npc.Key}`)",
            $"> {npc.Summary}",
            $"",
            $"- 标签: {(npc.Tags.Count > 0 ? string.Join(", ", npc.Tags) : "无")}",
            $"- 亲密度: {npc.CurrentIntimacy():F1} / {npc.Intimacy:F1}",
            $"- 记忆深度: {npc.GetMemoryDepth()}",
            $"- 最后互动: {npc.LastInteractAt:yyyy-MM-dd HH:mm}",
        };

        // 附加记忆片段（最近5条）
        var memories = await _npcService.GetMemoriesAsync(key);
        if (memories.Count > 0)
        {
            lines.Add("");
            lines.Add("**记忆片段:**");
            foreach (var m in memories.Take(5))
            {
                var delta = m.IntimacyDelta >= 0 ? $"+{m.IntimacyDelta}" : $"{m.IntimacyDelta}";
                lines.Add($"- [{m.Timestamp:MM-dd HH:mm}] {m.Content} ({delta})");
            }
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    private async Task<CommandResult> HandleSearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new CommandResult { CommandReply = "请输入搜索关键词。用法: `/npc search 关键词`" };

        var results = await _npcService.SearchByKeywordAsync(keyword);
        if (results.Count == 0)
            return new CommandResult { CommandReply = $"没有找到与 `{keyword}` 相关的 NPC。" };

        var lines = new List<string> { $"**搜索结果: {keyword}**", "" };
        foreach (var npc in results)
        {
            var tags = npc.Tags.Count > 0 ? string.Join(", ", npc.Tags) : "-";
            lines.Add($"- **{npc.Name}** (`{npc.Key}`) [{tags}] — {npc.Summary}");
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }
}
