// using System.ClientModel;
// using System.ClientModel.Primitives;
// using System.Text;
// using System.Text.Json;
//
// namespace Harem.Services.Agents.Policy;
//
// internal class DeepSeekV4Policy : PipelinePolicy
// {
//     // private static async Task<BinaryData> ReadBodyAsync(BinaryContent content)
//     // {
//     //     using var ms = new MemoryStream();
//     //     await content.WriteToAsync(ms);
//     //     return BinaryData.FromBytes(ms.ToArray());
//     // }
//     //
//     // private static BinaryData InjectThinkingDisabled(BinaryData body)
//     // {
//     //     using var doc = JsonDocument.Parse(body);
//     //     using var stream = new MemoryStream();
//     //     using (var writer = new Utf8JsonWriter(stream))
//     //     {
//     //         writer.WriteStartObject();
//     //         foreach (var prop in doc.RootElement.EnumerateObject())
//     //         {
//     //             prop.WriteTo(writer);
//     //         }
//     //
//     //         // 注入 thinking: {type: "disabled"}
//     //         writer.WriteStartObject("thinking");
//     //         writer.WriteString("type", "disabled");
//     //         writer.WriteEndObject();
//     //         writer.WriteEndObject();
//     //     }
//     //
//     //     return BinaryData.FromBytes(stream.ToArray());
//     // }
//     //
//     // public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
//     // {
//     //     ProcessNext(message, pipeline, currentIndex);
//     // }
//     //
//     // public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline,
//     //     int currentIndex)
//     // {
//     //     if (message.Request.Content is not null
//     //         && message.Request.Uri?.AbsoluteUri?.Contains("/chat/completions") == true)
//     //     {
//     //         // 读取原始请求体
//     //         var bodyBinaryData = await ReadBodyAsync(message.Request.Content);
//     //
//     //         // 调试：输出完整请求体到临时文件（每次覆盖）
//     //         try
//     //         {
//     //             var raw = bodyBinaryData.ToArray();
//     //             var pretty = JsonSerializer.Serialize(
//     //                 JsonSerializer.Deserialize<JsonElement>(raw),
//     //                 new JsonSerializerOptions { WriteIndented = true }
//     //             );
//     //             await File.WriteAllTextAsync("/tmp/hermes_debug_request.json", pretty);
//     //         }
//     //         catch
//     //         {
//     //         }
//     //
//     //         // 注入 thinking 参数
//     //         var modifiedBody = InjectThinkingDisabled(bodyBinaryData);
//     //         // 替换请求体
//     //         message.Request.Content = BinaryContent.Create(modifiedBody);
//     //     }
//     //
//     //     await ProcessNextAsync(message, pipeline, currentIndex);
//     // }
//
//     // 缓存上一轮 response 的 reasoning_content（简单方案，单会话场景下安全）
//     private string? _lastReasoningContent;
//
//     private static async Task<BinaryData> ReadBodyAsync(BinaryContent content)
//     {
//         using var ms = new MemoryStream();
//         await content.WriteToAsync(ms);
//         return BinaryData.FromBytes(ms.ToArray());
//     }
//
//     // ===== 请求注入：给所有 assistant message 加上 reasoning_content =====
//     // DeepSeek 要求 thinking mode 下每个 assistant message 都必须携带 reasoning_content 字段。
//     // - 有缓存时注入缓存值（来自上一轮 SSE 流提取）
//     // - 无缓存时注入空字符串（字段必须存在，值可以为空）
//     private BinaryData InjectReasoningContent(BinaryData body)
//     {
//         // 始终注入，不再因缓存为空而跳过
//         var cached = _lastReasoningContent ?? "";
//
//         using var doc = JsonDocument.Parse(body);
//         using var stream = new MemoryStream();
//         using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
//
//         writer.WriteStartObject();
//         foreach (var prop in doc.RootElement.EnumerateObject())
//         {
//             if (prop.NameEquals("messages"u8))
//             {
//                 writer.WritePropertyName("messages");
//                 writer.WriteStartArray();
//
//                 var messages = prop.Value.EnumerateArray().ToArray();
//                 for (int i = 0; i < messages.Length; i++)
//                 {
//                     var msg = messages[i];
//                     var isAssistant = msg.TryGetProperty("role", out var role)
//                                       && role.ValueKind == JsonValueKind.String
//                                       && role.GetString() == "assistant";
//
//                     if (isAssistant)
//                     {
//                         // 所有 assistant message 都注入 reasoning_content
//                         // （没有 tool_call 的会被 DeepSeek 忽略，有 tool_call 的必须传）
//                         writer.WriteStartObject();
//                         foreach (var msgProp in msg.EnumerateObject())
//                         {
//                             // 如果已经有了 reasoning_content 就保留原值
//                             if (msgProp.NameEquals("reasoning_content"u8))
//                                 continue;
//                             msgProp.WriteTo(writer);
//                         }
//
//                         writer.WriteString("reasoning_content", cached);
//                         writer.WriteEndObject();
//                     }
//                     else
//                     {
//                         msg.WriteTo(writer);
//                     }
//                 }
//
//                 writer.WriteEndArray();
//             }
//             else
//             {
//                 prop.WriteTo(writer);
//             }
//         }
//
//         writer.WriteEndObject();
//         writer.Flush();
//
//         return BinaryData.FromBytes(stream.ToArray());
//     }
//
//     public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
//     {
//         ProcessNext(message, pipeline, currentIndex);
//     }
//
//     public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline,
//         int currentIndex)
//     {
//         var isChatCompletions = message.Request.Content is not null
//                                 && message.Request.Uri?.AbsoluteUri.Contains("/chat/completions") == true;
//
//         // ========== 请求路径 (发出前) ==========
//         if (isChatCompletions && message.Request.Content is not null)
//         {
//             var bodyBinaryData = await ReadBodyAsync(message.Request.Content);
//
//             // 注入缓存中的 reasoning_content（从上一轮响应中提取的）
//             bodyBinaryData = InjectReasoningContent(bodyBinaryData);
//
//             message.Request.Content = BinaryContent.Create(bodyBinaryData);
//         }
//
//         await ProcessNextAsync(message, pipeline, currentIndex);
//
//         // ========== 响应路径 (收到后) ==========
//         // 只对 SSE 流式响应包裹 ReasoningContentStream
//         var isStreaming = false;
//         if (message.Response?.Headers is not null)
//         {
//             isStreaming = message.Response.Headers.TryGetValue("Content-Type", out var ct)
//                           && ct?.Contains("text/event-stream") == true;
//         }
//
//         if (isChatCompletions && isStreaming && message.Response?.ContentStream is not null)
//         {
//             // 用包裹流透明地提取 reasoning_content
//             message.Response.ContentStream = new ReasoningContentStream(
//                 message.Response.ContentStream,
//                 reasoningContent => _lastReasoningContent = reasoningContent
//             );
//         }
//     }
//
//     /// <summary>
//     /// 包裹 SSE 响应流的包装流。
//     /// 数据原样透传给上游（SDK 的 SSE 解析器），同时在通过时提取 reasoning_content。
//     /// </summary>
//     private sealed class ReasoningContentStream : Stream
//     {
//         private readonly Stream _inner;
//         private readonly Action<string> _onReasoningContent;
//         private readonly StringBuilder _reasoningBuffer = new();
//         private byte[] _lineBuffer = new byte[8192];
//         private int _linePos;
//         private bool _lineComplete;
//
//         public ReasoningContentStream(Stream inner, Action<string> onReasoningContent)
//         {
//             _inner = inner;
//             _onReasoningContent = onReasoningContent;
//         }
//
//         public override bool CanRead => true;
//         public override bool CanSeek => false;
//         public override bool CanWrite => false;
//         public override long Length => throw new NotSupportedException();
//
//         public override long Position
//         {
//             get => throw new NotSupportedException();
//             set => throw new NotSupportedException();
//         }
//
//         public override int Read(byte[] buffer, int offset, int count)
//         {
//             var read = _inner.Read(buffer, offset, count);
//             if (read > 0) ProcessBuffer(buffer, offset, read);
//             return read;
//         }
//
//         public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
//         {
//             var read = await _inner.ReadAsync(buffer, offset, count, ct);
//             if (read > 0) ProcessBuffer(buffer, offset, read);
//             return read;
//         }
//
// #if NET6_0_OR_GREATER
//         public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
//         {
//             var read = await _inner.ReadAsync(buffer, ct);
//             if (read > 0)
//             {
//                 var segment = buffer.Slice(0, read);
//                 ProcessBuffer(segment.Span);
//             }
//
//             return read;
//         }
// #endif
//
//         private void ProcessBuffer(byte[] buffer, int offset, int count)
//         {
//             var span = buffer.AsSpan(offset, count);
//             ProcessBuffer(span);
//         }
//
//         private void ProcessBuffer(ReadOnlySpan<byte> span)
//         {
//             foreach (var b in span)
//             {
//                 if (b == '\n')
//                 {
//                     ProcessLine();
//                     _linePos = 0;
//                     _lineComplete = true;
//                 }
//                 else if (b == '\r')
//                 {
//                     // skip CR, process on LF
//                     _lineComplete = false;
//                 }
//                 else
//                 {
//                     if (_linePos >= _lineBuffer.Length)
//                         Array.Resize(ref _lineBuffer, _lineBuffer.Length * 2);
//                     _lineBuffer[_linePos++] = b;
//                     _lineComplete = false;
//                 }
//             }
//         }
//
//         private void ProcessLine()
//         {
//             if (_linePos == 0) return;
//
//             var line = Encoding.UTF8.GetString(_lineBuffer, 0, _linePos);
//             const string dataPrefix = "data: ";
//             if (!line.StartsWith(dataPrefix)) return;
//
//             var jsonStr = line.Substring(dataPrefix.Length).Trim();
//             if (jsonStr == "[DONE]") return;
//
//             try
//             {
//                 using var doc = JsonDocument.Parse(jsonStr);
//                 if (doc.RootElement.TryGetProperty("choices", out var choices)
//                     && choices.ValueKind == JsonValueKind.Array
//                     && choices.GetArrayLength() > 0
//                     && choices[0].TryGetProperty("delta", out var delta)
//                     && delta.TryGetProperty("reasoning_content", out var rc)
//                     && rc.ValueKind == JsonValueKind.String)
//                 {
//                     var val = rc.GetString();
//                     if (!string.IsNullOrEmpty(val))
//                     {
//                         _reasoningBuffer.Append(val);
//                     }
//                 }
//             }
//             catch
//             {
//                 // 忽略解析失败的行
//             }
//         }
//
//         public override void Flush() => _inner.Flush();
//         public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
//         public override void SetLength(long value) => throw new NotSupportedException();
//         public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
//
//         protected override void Dispose(bool disposing)
//         {
//             if (disposing)
//             {
//                 // 流读完/关闭时，通知 Accumulated 结果
//                 if (_reasoningBuffer.Length > 0)
//                 {
//                     _onReasoningContent(_reasoningBuffer.ToString());
//                 }
//
//                 _inner.Dispose();
//             }
//
//             base.Dispose(disposing);
//         }
//     }
// }