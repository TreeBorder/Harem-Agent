namespace Harem.Contracts.Domain.Entities;

public class CommandResult
{
    public string? SystemMessage { get; set; }
    public string? UserMessage { get; set; }
    public string? CommandReply { get; set; }

    /// <summary>
    /// 切换目标模型 Key，格式 "Provider/Model"，非空时触发 Agent 重建
    /// </summary>
    public string? SwitchModelKey { get; set; }
}