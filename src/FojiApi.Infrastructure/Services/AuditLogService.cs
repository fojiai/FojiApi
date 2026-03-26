using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class AuditLogService(FojiDbContext db) : IAuditLogService
{
    public async Task<AuditLogPageResult> GetLogsAsync(int? companyId, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AuditLogs.Include(a => a.User).AsQueryable();
        if (companyId.HasValue) query = query.Where(a => a.CompanyId == companyId.Value);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItem(
                a.Id, a.Action, a.Resource, a.ResourceId,
                a.User != null ? $"{a.User.FirstName} {a.User.LastName}" : null,
                a.IpAddress, a.CreatedAt))
            .ToListAsync();

        return new AuditLogPageResult(total, page, pageSize, logs);
    }
}
