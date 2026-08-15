using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Core.Utilities;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class CrmTaskService(FojiDbContext db) : ICrmTaskService
{
    public async Task<IEnumerable<CrmTaskItem>> GetTasksAsync(
        int companyId, int? assigneeUserId = null, CrmTaskStatus? status = null,
        int? contactId = null, int? dealId = null)
    {
        var query = db.CrmTasks.Where(x => x.CompanyId == companyId);

        if (assigneeUserId.HasValue) query = query.Where(x => x.AssigneeUserId == assigneeUserId.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (contactId.HasValue) query = query.Where(x => x.ContactId == contactId.Value);
        if (dealId.HasValue) query = query.Where(x => x.DealId == dealId.Value);

        return await query
            // Open first, then by due date (nulls last), then newest.
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueAt == null)
            .ThenBy(x => x.DueAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(Map)
            .ToListAsync();
    }

    public async Task<CrmTaskItem> CreateTaskAsync(int companyId, CrmTaskInput input)
    {
        await ValidateLinksAsync(companyId, input.ContactId, input.DealId, input.AssigneeUserId);

        var task = new CrmTask
        {
            CompanyId = companyId,
            ContactId = input.ContactId,
            DealId = input.DealId,
            Title = input.Title.Trim(),
            Description = input.Description,
            Type = input.Type,
            Priority = input.Priority,
            Status = CrmTaskStatus.Open,
            DueAt = DateTimeUtc.Normalize(input.DueAt),
            AssigneeUserId = input.AssigneeUserId,
        };
        db.CrmTasks.Add(task);
        await db.SaveChangesAsync();
        return (await GetByIdAsync(companyId, task.Id))!;
    }

    public async Task<CrmTaskItem?> UpdateTaskAsync(int companyId, int taskId, CrmTaskInput input)
    {
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == taskId);
        if (task == null) return null;

        await ValidateLinksAsync(companyId, input.ContactId, input.DealId, input.AssigneeUserId);

        task.ContactId = input.ContactId;
        task.DealId = input.DealId;
        task.Title = input.Title.Trim();
        task.Description = input.Description;
        task.Type = input.Type;
        task.Priority = input.Priority;
        task.DueAt = DateTimeUtc.Normalize(input.DueAt);
        task.AssigneeUserId = input.AssigneeUserId;

        await db.SaveChangesAsync();
        return await GetByIdAsync(companyId, taskId);
    }

    public async Task<CrmTaskItem?> SetStatusAsync(int companyId, int taskId, CrmTaskStatus status)
    {
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == taskId);
        if (task == null) return null;

        task.Status = status;
        task.CompletedAt = status == CrmTaskStatus.Done ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        return await GetByIdAsync(companyId, taskId);
    }

    public async Task<bool> DeleteTaskAsync(int companyId, int taskId)
    {
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == taskId);
        if (task == null) return false;
        db.CrmTasks.Remove(task);
        await db.SaveChangesAsync();
        return true;
    }

    private Task<CrmTaskItem?> GetByIdAsync(int companyId, int taskId) =>
        db.CrmTasks.Where(x => x.CompanyId == companyId && x.Id == taskId).Select(Map).FirstOrDefaultAsync();

    private async Task ValidateLinksAsync(int companyId, int? contactId, int? dealId, int? assigneeUserId)
    {
        if (contactId != null && !await db.Contacts.AnyAsync(c => c.CompanyId == companyId && c.Id == contactId))
            throw new NotFoundException("Contact not found.");
        if (dealId != null && !await db.Deals.AnyAsync(d => d.CompanyId == companyId && d.Id == dealId))
            throw new NotFoundException("Deal not found.");
        if (assigneeUserId != null && !await db.UserCompanies.AnyAsync(uc =>
                uc.CompanyId == companyId && uc.UserId == assigneeUserId.Value && uc.IsActive))
            throw new DomainException("The selected assignee is not an active member of this company.");
    }

    private static System.Linq.Expressions.Expression<Func<CrmTask, CrmTaskItem>> Map => x => new CrmTaskItem(
        x.Id, x.ContactId, x.Contact != null ? x.Contact.Name : null,
        x.DealId, x.Deal != null ? x.Deal.Title : null,
        x.Title, x.Description, x.Type.ToString(), x.Priority.ToString(), x.Status.ToString(),
        x.DueAt, x.AssigneeUserId,
        x.Assignee != null ? (x.Assignee.FirstName + " " + x.Assignee.LastName).Trim() : null,
        x.CompletedAt, x.CreatedAt);
}
