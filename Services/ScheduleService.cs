using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nexus.Models.Schedule;
using Nexus.Services.Http;

namespace Nexus.Services;

public class ScheduleService : HttpService
{
    public event Action<string>? StatusChanged;
    public event Action<bool>? LoadingStateChanged;

    public ScheduleService(ConfigService configService, ToastService? toastService = null) 
        : base(configService, toastService)
    {
    }

    public async Task<ScheduleCompletenessModel?> CheckCompletenessAsync(int classId)
    {
        System.Diagnostics.Debug.WriteLine($"[ScheduleService] CheckCompletenessAsync 开始, classId={classId}");

        LoadingStateChanged?.Invoke(true);
        StatusChanged?.Invoke("正在检查排班配置...");

        var response = await GetAsync<ScheduleCompletenessModel>(
            $"/desktop/schedule/completeness?class_id={classId}",
            new RequestOptions { OperationName = "检查排班配置" });

        LoadingStateChanged?.Invoke(false);

        if (response?.IsSuccess == true && response.Data != null)
        {
            StatusChanged?.Invoke(response.Data.IsComplete ? "排班配置完整" : "排班配置不完整");
            return response.Data;
        }

        StatusChanged?.Invoke(response?.Msg ?? "检查失败");
        return null;
    }

    public async Task<List<PeriodicRuleModel>?> GetPeriodicRulesAsync(int classId)
    {
        LoadingStateChanged?.Invoke(true);

        var response = await GetAsync<List<PeriodicRuleModel>>(
            $"/desktop/periodic-rules?class_id={classId}",
            new RequestOptions { OperationName = "获取排班规则" });

        LoadingStateChanged?.Invoke(false);

        return response?.Data;
    }

    public async Task<PeriodicRuleModel?> AddPeriodicRuleAsync(AddPeriodicRuleRequest request)
    {
        LoadingStateChanged?.Invoke(true);

        var response = await PostAsync<PeriodicRuleModel>(
            "/desktop/periodic-rules",
            request,
            new RequestOptions { OperationName = "添加排班规则" });

        LoadingStateChanged?.Invoke(false);

        if (response?.IsSuccess == true)
        {
            StatusChanged?.Invoke("添加成功");
            return response.Data;
        }

        StatusChanged?.Invoke(response?.Msg ?? "添加失败");
        return null;
    }

    public async Task<BatchAddResult?> BatchAddPeriodicRulesAsync(int classId, List<AddPeriodicRuleRequest> rules)
    {
        LoadingStateChanged?.Invoke(true);
        StatusChanged?.Invoke("正在批量保存排班规则...");

        var request = new BatchAddPeriodicRulesRequest
        {
            ClassId = classId,
            Rules = rules
        };

        var response = await PostAsync<BatchAddResult>(
            "/desktop/periodic-rules/batch",
            request,
            new RequestOptions { OperationName = "批量添加排班规则" });

        LoadingStateChanged?.Invoke(false);

        if (response?.IsSuccess == true)
        {
            StatusChanged?.Invoke(response.Msg);
            return response.Data;
        }

        StatusChanged?.Invoke(response?.Msg ?? "批量添加失败");
        return null;
    }

    public async Task<bool> UpdatePeriodicRuleAsync(int ruleId, UpdatePeriodicRuleRequest request)
    {
        LoadingStateChanged?.Invoke(true);

        var response = await PutAsync<bool>(
            $"/desktop/periodic-rules/{ruleId}",
            request,
            new RequestOptions { OperationName = "更新排班规则" });

        LoadingStateChanged?.Invoke(false);

        if (response?.IsSuccess == true)
        {
            StatusChanged?.Invoke("更新成功");
            return true;
        }

        StatusChanged?.Invoke(response?.Msg ?? "更新失败");
        return false;
    }

    public async Task<bool> DeletePeriodicRuleAsync(int ruleId)
    {
        LoadingStateChanged?.Invoke(true);

        var response = await DeleteAsync(
            $"/desktop/periodic-rules/{ruleId}",
            new RequestOptions { OperationName = "删除排班规则" });

        LoadingStateChanged?.Invoke(false);

        if (response?.IsSuccess == true)
        {
            StatusChanged?.Invoke("删除成功");
            return true;
        }

        StatusChanged?.Invoke(response?.Msg ?? "删除失败");
        return false;
    }

    public async Task<List<TimeSlotModel>?> GetFixedTimeSlotsAsync()
    {
        var response = await GetAsync<List<TimeSlotModel>>(
            "/desktop/time-slots/fixed",
            new RequestOptions { OperationName = "获取固定时段" });

        return response?.Data;
    }

    public async Task<List<string>?> GetServiceTypesAsync()
    {
        var response = await GetAsync<List<string>>(
            "/desktop/service-types",
            new RequestOptions { OperationName = "获取服务类型", RequireAuth = false });

        return response?.Data;
    }

    public async Task<List<TeacherModel>?> GetTeachersAsync(string? search = null)
    {
        var endpoint = "/desktop/teachers";
        if (!string.IsNullOrEmpty(search))
        {
            endpoint += $"?search={Uri.EscapeDataString(search)}";
        }

        var response = await GetAsync<List<TeacherModel>>(
            endpoint,
            new RequestOptions { OperationName = "获取教师列表" });

        return response?.Data;
    }
}

#region Response Models

public class BatchAddResult
{
    [JsonPropertyName("added_count")]
    public int AddedCount { get; set; }
    
    [JsonPropertyName("skipped_count")]
    public int SkippedCount { get; set; }
    
    [JsonPropertyName("added_rules")]
    public List<PeriodicRuleModel>? AddedRules { get; set; }
    
    [JsonPropertyName("skipped_rules")]
    public List<object>? SkippedRules { get; set; }
}

#endregion
