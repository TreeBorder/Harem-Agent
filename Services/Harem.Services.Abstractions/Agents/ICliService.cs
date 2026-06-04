namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 通用命令行执行服务
/// 封装 Process 启动、等待、输出捕获等通用逻辑
/// </summary>
public interface ICliService
{
    /// <summary>
    /// 执行命令行并返回结果
    /// </summary>
    Task<CliResult> ExecuteAsync(CliRequest request, CancellationToken ct = default);
}

/// <summary>
/// 命令行执行请求
/// </summary>
public class CliRequest
{
    /// <summary>
    /// 可执行文件绝对路径
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 参数列表
    /// </summary>
    public List<string> Arguments { get; set; } = [];

    /// <summary>
    /// 工作目录（可选）
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 环境变量覆盖（可选）
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
}

/// <summary>
/// 命令行执行结果
/// </summary>
public class CliResult
{
    /// <summary>
    /// 进程退出码
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 标准输出
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// 标准错误
    /// </summary>
    public string StandardError { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功（退出码为 0）
    /// </summary>
    public bool IsSuccess => ExitCode == 0;
}
