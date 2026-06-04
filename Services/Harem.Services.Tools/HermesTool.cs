using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Abstractions.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Harem.Services.Tools;

/// <summary>
/// Hermes Agent (Clio) 工具实现
/// 异步调用 clio chat CLI，结果通过消息通道推回给 Agent
/// </summary>
public class HermesTool : IHermesTool
{
    private readonly IClioClient _clioClient;
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly IMessageChannel<ReceivedMessage> _receiveChannel;
    private readonly ISessionService _sessionService;
    private readonly ILogger<HermesTool> _logger;

    public HermesTool(
        IClioClient clioClient,
        IMessageChannel<ReplyMessage> replyChannel,
        IMessageChannel<ReceivedMessage> receiveChannel,
        ISessionService sessionService,
        ILogger<HermesTool> logger)
    {
        _clioClient = clioClient;
        _replyChannel = replyChannel;
        _receiveChannel = receiveChannel;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<string> Chat(string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "查询内容不能为空";

        // 获取当前会话上下文
        var session = AIAgent.CurrentRunContext?.Session;
        if (session == null)
            return "获取上下文失败";

        var sessionKey = session.StateBag.GetValue<string>("sessionKey");
        if (string.IsNullOrEmpty(sessionKey))
            return "获取会话密钥失败";

        var sessionInfo = await _sessionService.GetSessionAsync(sessionKey);

        var channelId = sessionInfo.Info.ChannelId;

        // 通知用户：开始执行
        await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, "🤖 Clio 开始执行任务"));

        // 启动后台任务执行 CLI
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Clio 子智能体开始执行: Query={Query}, SessionId={SessionId}", query, sessionId);

                var extraArgs = !string.IsNullOrWhiteSpace(sessionId)
                    ? new[] { "--resume", sessionId }
                    : null;

                var result = await _clioClient.ChatAsync(query, extraArgs);

                string resultText;
                if (!result.IsSuccess)
                {
                    resultText = $"Clio 执行失败：{result.Error}";
                    _logger.LogError("Clio 子智能体执行失败: ExitCode={ExitCode}, Error={Error}", result.ExitCode, result.Error);
                }
                else
                {
                    resultText = string.IsNullOrWhiteSpace(result.Output) ? "Clio 无响应" : result.Output;
                }

                // 通过系统消息推送给 Agent（触发 LLM 处理结果）
                var systemMsg = $"[子智能体任务结果]{resultText}[/]";
                await _receiveChannel.WriteAsync(new ReceivedMessage(sessionKey, channelId, systemMsg));

                // 通知用户：执行完成
                await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, "✅ Clio 任务已完成"));

                _logger.LogInformation("Clio 子智能体执行完成，结果已推送");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clio 子智能体执行异常");

                var errorMsg = $"[子智能体任务结果]Clio 执行异常：{ex.Message}[/]";
                await _receiveChannel.WriteAsync(new ReceivedMessage(sessionKey, channelId, errorMsg));
            }
        });

        return "已派 Clio 去执行任务，结果完成后会通过系统消息推送";
    }
}
