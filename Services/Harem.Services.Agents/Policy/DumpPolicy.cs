using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Harem.Services.Agents.Policy;

public class DumpPolicy : PipelinePolicy
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<BinaryData> ReadBodyAsync(BinaryContent content)
    {
        using var ms = new MemoryStream();
        await content.WriteToAsync(ms);
        return BinaryData.FromBytes(ms.ToArray());
    }

    private static void DumpJson(BinaryData data, string path)
    {
        try
        {
            var pretty = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<JsonElement>(data),
                PrettyOptions
            );
            File.WriteAllText(path, pretty);
        }
        catch (Exception err)
        {
            Console.WriteLine($"[DumpPolicy] 写入 {path} 失败: {err.Message}");
        }
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        // ===== 请求路径：dump 请求体 =====
        if (message.Request.Content is not null)
        {
            var body = await ReadBodyAsync(message.Request.Content);
            DumpJson(body, "/tmp/harem_request.json");
        }

        // ===== 放行请求 =====
        await ProcessNextAsync(message, pipeline, currentIndex);

        // ===== 响应路径：dump 响应体 =====
        if (message.Response?.Content is not null)
        {
            try
            {
                // PipelineResponse.Content 是 BinaryData，不是 BinaryContent
                var binaryData = message.Response.Content;
                DumpJson(binaryData, "/tmp/harem_response.json");
            }
            catch (Exception err)
            {
                Console.WriteLine($"[DumpPolicy] 读取响应失败: {err.Message}");
            }
        }
    }
}