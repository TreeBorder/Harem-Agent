namespace Harem.Services.Agents.Handler;

internal static class ExceptionHandler
{
    /// <summary>
    /// 判断异常是否可恢复（可回退）：网络错误、速率限制、服务过载等
    /// </summary>
    public static bool IsRecoverableException(Exception ex, CancellationToken ct = default)
    {
        // HttpClient 请求异常（网络错误、连接超时）
        if (ex is HttpRequestException)
            return true;

        // TaskCanceledException 且非用户主动取消 = 超时/网络断开
        // HttpClient 内部超时抛出的 TCE，其 CancellationToken 是内部 CTS（已被取消），
        // 不能通过 ex.CancellationToken 判断，必须以外部 ct 是否被取消为准
        if (ex is TaskCanceledException && !ct.IsCancellationRequested)
            return true;

        // Anthropic SDK 异常
        var exType = ex.GetType();
        if (exType.Name == "Anthropic5xxException")
            return true; // 529 overloaded、500 internal 等，均可回退
        if (exType.Name == "Anthropic4xxException")
        {
            // 429 rate limit 可回退，401/403 认证错误不可回退
            var statusProp = exType.GetProperty("StatusCode");
            if (statusProp?.GetValue(ex) is int status)
                return status is not (401 or 403);
            return true; // 无法读取状态码时保守地认为可回退
        }

        // OpenAI SDK 的 ClientResultException（含 429/500/502/503 等）
        if (exType.Name == "ClientResultException" || exType.Name == "OpenAIClientException")
        {
            var statusProp = exType.GetProperty("Status");
            if (statusProp?.GetValue(ex) is int status)
                return status is >= 400 and not 401 and not 403;
        }

        // 内部异常也检查
        return ex.InnerException != null && IsRecoverableException(ex.InnerException, ct);
    }

    /// <summary>
    /// 判断是否为瞬时过载错误（529 overloaded 等），值得短暂重试
    /// </summary>
    public static bool IsTransientOverload(Exception ex)
    {
        // Anthropic 5xx（529 overloaded 等）
        if (ex.GetType().Name == "Anthropic5xxException")
            return true;

        // OpenAI SDK 的 429/503
        var exType = ex.GetType();
        if (exType.Name == "ClientResultException" || exType.Name == "OpenAIClientException")
        {
            var statusProp = exType.GetProperty("Status");
            if (statusProp?.GetValue(ex) is int status)
                return status is 429 or 503 or 529;
        }

        return ex.InnerException != null && IsTransientOverload(ex.InnerException);
    }
}