using System.Diagnostics;
using Harem.Services.Abstractions.Agents;

namespace Harem.Services.Agents;

/// <summary>
/// 通用命令行执行服务实现
/// </summary>
public class CliService : ICliService
{
    public async Task<CliResult> ExecuteAsync(CliRequest request, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty
        };

        foreach (var arg in request.Arguments)
            psi.ArgumentList.Add(arg);

        if (request.EnvironmentVariables != null)
        {
            foreach (var kv in request.EnvironmentVariables)
                psi.EnvironmentVariables[kv.Key] = kv.Value;
        }

        using var process = new Process();
        process.StartInfo = psi;
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        return new CliResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = output,
            StandardError = error
        };
    }
}
