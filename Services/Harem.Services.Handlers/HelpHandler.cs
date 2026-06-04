using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class HelpHandler : ICommandHandler
{
    public const string CommandName = "help";
    public string Command => CommandName;

    public Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var text = """
                   **🎮 可用指令**

                   | 指令 | 说明 |
                   |------|------|
                   | `/help` | 显示帮助 |
                   | `/session [info]` | 查看当前会话详情 |
                   | `/session reset [内容]` | 重置会话，导出历史到天记忆，保留摘要 |
                   | `/session list` | 列出所有会话 |
                   | `/session systemsent` | 切换系统消息接收会话 |
                   | `/state` | 查看全部状态 |
                   | `/state get 键` | 查看单个状态 |
                   | `/state set 键 值` | 修改状态值 |
                   | `/npc [list]` | 列出所有 NPC |
                   | `/npc get 关键字` | 查看 NPC 详情 |
                   | `/npc search 关键词` | 搜索 NPC |
                   | `/memory search 关键词` | 语义搜索记忆 |
                   | `/memory index` | 手动构建记忆索引 |
                   | `/memory get [日期]` | 查看每日记忆 |
                   | `/memory longterm` | 查看长期记忆 |
                   | `/memory reexport` | 重新导出每日记忆 |
                   | `/photo [camera 动作]` | 生成角色图片 |
                   | `/photo gallery 场景 服装 动作` | 生成图集 |
                   | `/heartbeat` | 手动触发心跳 |
                   | `/fix summary` | 已移至 `/summary` |
                   | `/summary update` | 增量摘要，将未摘要消息纳入 |
                   | `/summary force` | 强制重生成摘要，忽略已有信息 |
                   | `/keyword` | 查看当前关键词索引 |
                   | `/keyword extract YYYY-MM-DD` | 手动提取指定日期的关键词 |
                   | `/model [list]` | 列出可用模型 |
                   | `/model switch Provider/Model` | 切换模型 |
                   | `{NPC名/标签}` | 强制注入 NPC |
                   | `#关键词#` | 强制注入记忆 |
                   """;
        return Task.FromResult(new CommandResult { CommandReply = text });
    }
}