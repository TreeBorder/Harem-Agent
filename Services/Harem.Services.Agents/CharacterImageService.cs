using System.Text.Json.Nodes;
using Harem.Protocol.ComfyUI;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Harem.Services.Agents;

/// <summary>
/// 角色图片服务实现
/// </summary>
public class CharacterImageService : ICharacterImageService
{
    private readonly IComfyUiClientFactory _comfyUiClientFactory;
    private readonly IStateService _state;
    private readonly IWorkflowBuilderService _workflowBuilder;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<CharacterImageService> _logger;

    public CharacterImageService(IComfyUiClientFactory comfyUiClientFactory, IStateService stateService,
        IWorkflowBuilderService workflowBuilderService, ISchedulerFactory schedulerFactory,
        ILogger<CharacterImageService> logger)
    {
        _comfyUiClientFactory = comfyUiClientFactory;
        _state = stateService;
        _workflowBuilder = workflowBuilderService;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task<bool> GenerateCameraImageAsync(string sessionKey, string action, string? aspectRatio = null)
    {
        // 获取当前状态
        var scene = await _state.GetStateAsync("scene");
        if (scene == null) return false;
        var outfit = await _state.GetStateAsync("outfit");
        if (outfit == null) return false;

        var workflow = await _workflowBuilder.BuildAsync(scene.Value, outfit.Value, action, aspectRatio);
        if (string.IsNullOrEmpty(workflow))
        {
            return false;
        }

        // 提交到 ComfyUI
        var client = _comfyUiClientFactory.CreateClient();
        var workflowJson = JsonNode.Parse(workflow);
        var promptId = await client.QueuePromptAsync(workflowJson!);
        if (string.IsNullOrEmpty(promptId)) return false;
        _ = StartTrackingAsync(sessionKey, promptId);
        return true;
    }

    public async Task<bool> GenerateGalleryImageAsync(string sessionKey, string scene, string outfit, string action, string? aspectRatio = null)
    {
        var workflow = await _workflowBuilder.BuildAsync(scene, outfit, action, aspectRatio);
        if (string.IsNullOrEmpty(workflow))
        {
            return false;
        }

        // 提交到 ComfyUI
        var client = _comfyUiClientFactory.CreateClient();
        var workflowJson = JsonNode.Parse(workflow);
        var promptId = await client.QueuePromptAsync(workflowJson!);
        if (string.IsNullOrEmpty(promptId)) return false;
        _ = StartTrackingAsync(sessionKey, promptId);
        return true;
    }

    public async Task<bool> PreCheck(string action)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> PreCheck(string scene, string outfit, string action)
    {
        throw new NotImplementedException();
    }

    private async Task StartTrackingAsync(string sessionKey, string promptId)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var jobData = new JobDataMap
            {
                ["SessionKey"] = sessionKey,
                ["PromptId"] = promptId,
            };

            var job = JobBuilder.Create<CharacterImageTrackingJob>()
                .WithIdentity($"photo_task_{promptId}", "photo_tasks")
                .UsingJobData(jobData)
                .Build();

            // 首轮400秒后执行，后续每20秒一次
            var trigger = TriggerBuilder.Create()
                .WithIdentity($"photo_trigger_{promptId}", "photo_triggers")
                .StartAt(DateTimeOffset.Now.AddSeconds(400))
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(20)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            _logger.LogInformation(
                "Started tracking photo task {promptId} , first check in 400s",
                promptId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start tracking for task {promptId}", promptId);
        }
    }
}