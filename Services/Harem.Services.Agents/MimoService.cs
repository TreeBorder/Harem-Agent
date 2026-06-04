using Harem.Contracts.Configurations;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

/// <summary>
/// MiMo TTS 语音合成服务实现 — 封装 CLI 调用逻辑
/// </summary>
public class MimoService : IMimoService
{
    private readonly ICliService _cliService;
    private readonly string _mimoPath;
    private readonly string _apiKey;
    private readonly string _workspaceDirectory;
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly ILogger<MimoService> _logger;

    private const string AudioDirectory = ".audio";

    public MimoService(
        IOptions<AgentOptions> agentOptions,
        IOptions<RuntimeOptions> runtimeOptions,
        IMessageChannel<ReplyMessage> replyChannel,
        ICliService cliService,
        ILogger<MimoService> logger)
    {
        var mimoVoice = agentOptions.Value.MimoVoice;
        _mimoPath = mimoVoice?.Path ?? "mimo";
        _apiKey = mimoVoice?.ApiKey ?? string.Empty;
        _workspaceDirectory = runtimeOptions.Value.Workspace;
        _replyChannel = replyChannel;
        _cliService = cliService;
        _logger = logger;
    }

    public async Task<bool> SynthesizeAndSendAsync(string text, string? designPrompt, string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("MiMo 语音合成跳过：文本为空");
            return false;
        }

        try
        {
            // 确保 .audio 目录存在
            var audioDir = Path.Combine(_workspaceDirectory, AudioDirectory);
            if (!Directory.Exists(audioDir))
                Directory.CreateDirectory(audioDir);

            var fileName = $"{Guid.NewGuid():N}.wav";
            var outputPath = Path.Combine(audioDir, fileName);

            var isDesign = !string.IsNullOrWhiteSpace(designPrompt);

            // 构建 CLI 参数
            var args = new List<string>();
            if (isDesign)
            {
                args.AddRange(["tts", "design", text, "-o", outputPath, "--prompt", designPrompt!]);
            }
            else
            {
                args.AddRange(["tts", "speak", text, "-o", outputPath]);
            }

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                args.Add("--api-key");
                args.Add(_apiKey);
            }

            _logger.LogInformation(
                "MiMo 语音合成: IsDesign={IsDesign}, OutputPath={OutputPath}",
                isDesign, outputPath);

            var request = new CliRequest
            {
                FileName = _mimoPath,
                Arguments = args
            };

            var result = await _cliService.ExecuteAsync(request);

            if (!result.IsSuccess)
            {
                _logger.LogError("MiMo 语音合成失败 (ExitCode={ExitCode}): {Stderr}",
                    result.ExitCode, result.StandardError);
                return false;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogError("MiMo 语音合成失败：输出文件未生成");
                return false;
            }

            await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, "", [outputPath]));
            _logger.LogInformation("MiMo 语音已发送: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MiMo 语音合成异常");
            return false;
        }
    }
}