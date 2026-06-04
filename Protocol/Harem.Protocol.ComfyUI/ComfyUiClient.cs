using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Harem.Protocol.ComfyUI;

public class ComfyUiClient(HttpClient httpClient, string baseUrl)
{
    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    /// <summary>
    /// 提交生成任务到 ComfyUI
    /// </summary>
    public async Task<string?> QueuePromptAsync(JsonNode workflow, CancellationToken cancellationToken = default)
    {
        var requestBody = new JsonObject
        {
            ["prompt"] = workflow
        };

        var content = new StringContent(
            requestBody.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync($"{_baseUrl}/prompt", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<JsonObject>(responseJson);

        return result?["prompt_id"]?.GetValue<string>();
    }

    /// <summary>
    /// 获取任务历史记录（用于查询完成状态）
    /// </summary>
    public async Task<JsonObject?> GetHistoryAsync(string promptId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{_baseUrl}/history/{promptId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JsonObject>(responseJson);
    }

    /// <summary>
    /// 获取队列状态
    /// </summary>
    public async Task<JsonObject?> GetQueueStatus(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{_baseUrl}/queue", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JsonObject>(responseJson);
    }

    /// <summary>
    /// 构建图片访问 URL
    /// </summary>
    private string BuildImageUrl(string filename, string? subfolder = null, string type = "output")
    {
        var url = $"{_baseUrl}/view?filename={Uri.EscapeDataString(filename)}";
        if (!string.IsNullOrEmpty(subfolder))
        {
            url += $"&subfolder={Uri.EscapeDataString(subfolder)}";
        }

        url += $"&type={Uri.EscapeDataString(type)}";
        return url;
    }

    /// <summary>
    /// 从历史记录中提取图片 URL 列表
    /// </summary>
    public string[] ExtractImageUrls(JsonObject history, string promptId)
    {
        var images = new List<string>();

        var promptData = history[promptId];
        if (promptData == null) return images.ToArray();

        var outputs = promptData["outputs"]?.AsObject();
        if (outputs == null) return images.ToArray();

        foreach (var node in outputs)
        {
            var nodeImages = node.Value?["images"]?.AsArray();
            if (nodeImages == null) continue;

            foreach (var img in nodeImages)
            {
                var filename = img?["filename"]?.GetValue<string>();
                if (string.IsNullOrEmpty(filename)) continue;

                var subfolder = img?["subfolder"]?.GetValue<string>();
                var type = img?["type"]?.GetValue<string>() ?? "output";

                images.Add(BuildImageUrl(filename, subfolder, type));
            }
        }

        return images.ToArray();
    }
}