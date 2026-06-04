namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// Clio（Hermes 子智能体）进程调用客户端
/// 封装 clio chat CLI 的进程启动、PATH 注入、输出读取等通用逻辑
/// 供 KeywordService、HermesTool 及其他需要委托 Clio 的模块复用
/// </summary>
public interface IClioClient
{
    /// <summary>
    /// 以 clio chat [-q <query>|-Q] 方式执行查询
    /// 也可通过 extraArgs 传入 --resume 等前置参数，拼在 -q 之前
    /// </summary>
    Task<ClioResult> ChatAsync(string query, IEnumerable<string>? extraArgs = null, CancellationToken ct = default);
}

/// <summary>
/// Clio 进程调用的执行结果
/// </summary>
/// <param name="ExitCode">进程退出码，0 表示成功</param>
/// <param name="Output">标准输出内容</param>
/// <param name="Error">标准错误内容</param>
public record ClioResult(int ExitCode, string Output, string Error)
{
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// 成功时取 Output，失败时取 Error 作为提示
    /// </summary>
    public string OutputOrError => IsSuccess ? Output : Error;
}
