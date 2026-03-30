namespace FojiApi.Core.Interfaces.Services;

public interface IPlatformSettingService
{
    Task<IEnumerable<PlatformSettingResult>> GetAllAsync();
    Task<PlatformSettingResult> UpsertAsync(string key, string value, string label, string category, bool isSecret);
    Task DeleteAsync(string key);
    Task<string?> GetValueAsync(string key);
}

public record PlatformSettingResult(
    int Id, string Key, string Value, string Label, string Category, bool IsSecret, DateTime UpdatedAt
);
