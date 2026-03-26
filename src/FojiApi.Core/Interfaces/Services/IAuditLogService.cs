namespace FojiApi.Core.Interfaces.Services;

public interface IAuditLogService
{
    Task<AuditLogPageResult> GetLogsAsync(int? companyId, int page, int pageSize);
}

public record AuditLogPageResult(int Total, int Page, int PageSize, IEnumerable<AuditLogItem> Data);
public record AuditLogItem(int Id, string Action, string Resource, string? ResourceId, string? UserName, string? IpAddress, DateTime CreatedAt);
