using System.Diagnostics;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

/// <summary>
/// Clio（Hermes 子智能体）进程调用客户端
/// 封装 clio chat CLI 的启动、PATH 注入、输出读取等通用逻辑
/// </summary>
public class ClioClient : IClioClient
{
    private readonly string _clioPath;
    private readonly string? _binPath;
    private readonly ILogger<ClioClient> _logger;

    public ClioClient(
        IOptions<AgentOptions> agentOptions,
        ILogger<ClioClient> logger)
    {
        _clioPath = agentOptions.Value.SubAgent?.ClioPath ?? "clio";
        _binPath = agentOptions.Value.SubAgent?.BinPath;
        _logger = logger;
    }

    public async Task<ClioResult> ChatAsync(string query, IEnumerable<string>? extraArgs = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _clioPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("chat");

        if (extraArgs != null)
        {
            foreach (var arg in extraArgs)
                psi.ArgumentList.Add(arg);
        }

        psi.ArgumentList.Add("-q");
        psi.ArgumentList.Add(query);
        psi.ArgumentList.Add("-Q");

        // Supervisor 环境 PATH 精简，需手动注入 cli 工具所在目录
        var paths = new List<string>();
        if (!string.IsNullOrEmpty(_binPath)) paths.Add(_binPath);
        if (paths.Count > 0)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            psi.EnvironmentVariables["PATH"] = $"{string.Join(":", paths)}:{currentPath}";
        }

        using var process = new Process();
        process.StartInfo = psi;

        _logger.LogDebug("启动 Clio 子智能体: Query={Query}", query);

        process.Start();

        // 后台读取 stdout/stderr 防止缓冲区满导致死锁
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        _logger.LogDebug(
            "Clio 子智能体完成: ExitCode={ExitCode}, OutputLength={Len}, ErrorLength={ErrLen}",
            process.ExitCode, output?.Length ?? 0, error?.Length ?? 0);

        return new ClioResult(process.ExitCode, output?.Trim() ?? "", error?.Trim() ?? "");
    }
}
