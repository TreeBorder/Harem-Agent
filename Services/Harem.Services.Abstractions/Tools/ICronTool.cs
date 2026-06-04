using System.ComponentModel;

namespace Harem.Services.Abstractions.Tools;

/// <summary>
/// 定时任务工具 - AI 可调用
/// </summary>
[Description("定时任务工具，用于创建和管理定时任务")]
public interface ICronTool
{
    [Description("创建循环定时任务。target: SystemToAgent=系统消息给Agent, MessageToUser=消息给用户, MessageToAgent=用户消息给Agent")]
    Task<string> CreateCronJob(string name, string cronExpression, string target, string content);

    [Description("创建单次定时任务。fireAt格式: yyyy-MM-dd HH:mm:ss")]
    Task<string> CreateOneShotJob(string name, string fireAt, string target, string content);

    [Description("列出所有定时任务")]
    Task<string> ListCronJobs();

    [Description("删除定时任务")]
    Task<string> DeleteCronJob(string jobId);

    [Description("启用或禁用定时任务")]
    Task<string> ToggleCronJob(string jobId, bool enabled);
}
