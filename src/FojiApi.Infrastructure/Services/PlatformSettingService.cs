using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class PlatformSettingService(FojiDbContext db) : IPlatformSettingService
{
    public async Task<IEnumerable<PlatformSettingResult>> GetAllAsync()
    {
        return await db.PlatformSettings
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .Select(s => new PlatformSettingResult(
                s.Id, s.Key,
                s.IsSecret ? MaskValue(s.Value) : s.Value,
                s.Label, s.Category, s.IsSecret, s.UpdatedAt))
            .ToListAsync();
    }

    public async Task<PlatformSettingResult> UpsertAsync(string key, string value, string label, string category, bool isSecret)
    {
        var existing = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key);

        if (existing != null)
        {
            // If value is the masked placeholder, don't overwrite the real value
            if (!IsMaskedValue(value))
                existing.Value = value;
            existing.Label = label;
            existing.Category = category;
            existing.IsSecret = isSecret;
        }
        else
        {
            existing = new PlatformSetting
            {
                Key = key,
                Value = value,
                Label = label,
                Category = category,
                IsSecret = isSecret,
            };
            db.PlatformSettings.Add(existing);
        }

        await db.SaveChangesAsync();

        return new PlatformSettingResult(
            existing.Id, existing.Key,
            existing.IsSecret ? MaskValue(existing.Value) : existing.Value,
            existing.Label, existing.Category, existing.IsSecret, existing.UpdatedAt);
    }

    public async Task DeleteAsync(string key)
    {
        var setting = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key)
            ?? throw new NotFoundException($"Setting '{key}' not found.");
        db.PlatformSettings.Remove(setting);
        await db.SaveChangesAsync();
    }

    public async Task<string?> GetValueAsync(string key)
    {
        return await db.PlatformSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 8) return "••••••••";
        return value[..4] + "••••••••" + value[^4..];
    }

    private static bool IsMaskedValue(string value)
        => value.Contains("••••••••");
}
