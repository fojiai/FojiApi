using FojiApi.Core.Enums;

namespace FojiApi.Core.Interfaces.Services;

public interface ICrmTaskService
{
    Task<IEnumerable<CrmTaskItem>> GetTasksAsync(
        int companyId, int? assigneeUserId = null, CrmTaskStatus? status = null,
        int? contactId = null, int? dealId = null);

    Task<CrmTaskItem> CreateTaskAsync(int companyId, CrmTaskInput input);
    Task<CrmTaskItem?> UpdateTaskAsync(int companyId, int taskId, CrmTaskInput input);
    Task<CrmTaskItem?> SetStatusAsync(int companyId, int taskId, CrmTaskStatus status);
    Task<bool> DeleteTaskAsync(int companyId, int taskId);
}

public record CrmTaskItem(
    int Id,
    int? ContactId,
    string? ContactName,
    int? DealId,
    string? DealTitle,
    string Title,
    string? Description,
    string Type,
    string Priority,
    string Status,
    DateTime? DueAt,
    int? AssigneeUserId,
    string? AssigneeName,
    DateTime? CompletedAt,
    DateTime CreatedAt
);

public record CrmTaskInput(
    int? ContactId,
    int? DealId,
    string Title,
    string? Description,
    CrmTaskType Type,
    CrmTaskPriority Priority,
    DateTime? DueAt,
    int? AssigneeUserId
);
