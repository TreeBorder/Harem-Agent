using System.Text.Json.Serialization;

namespace Harem.Contracts.Domain.Entities;

public class PromptBlock
{
    /// <summary>
    /// 显示名称
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>
    /// 正向提示词
    /// SDXL: ["tag1", "tag2"]
    /// ZIT: ["段落文字描述"]
    /// </summary>
    [JsonPropertyName("prompt")]
    public List<string> PositivePrompts { get; set; } = [];

    /// <summary>
    /// 反向提示词
    /// </summary>
    [JsonPropertyName("negative")]
    public List<string> NegativePrompts { get; set; } = [];
}