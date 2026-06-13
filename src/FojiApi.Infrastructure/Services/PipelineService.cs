using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FojiApi.Infrastructure.Services;

public class PipelineService(FojiDbContext db) : IPipelineService
{
    // Default stages seeded for a company's first pipeline.
    private static readonly (string Name, bool IsWon, bool IsLost)[] DefaultStages =
    [
        ("New", false, false),
        ("Contacted", false, false),
        ("Proposal", false, false),
        ("Won", true, false),
        ("Lost", false, true),
    ];

    public async Task<PipelineDto> EnsureDefaultPipelineAsync(int companyId)
    {
        var existing = await db.Pipelines
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.IsDefault);
        if (existing != null)
            return (await GetPipelineAsync(companyId, existing.Id))!;

        var pipeline = new Pipeline { CompanyId = companyId, Name = "Sales", IsDefault = true, SortOrder = 0 };
        for (var i = 0; i < DefaultStages.Length; i++)
        {
            var (name, isWon, isLost) = DefaultStages[i];
            pipeline.Stages.Add(new PipelineStage
            {
                CompanyId = companyId, Name = name, SortOrder = i, IsWon = isWon, IsLost = isLost,
            });
        }
        db.Pipelines.Add(pipeline);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Lost the seed race — another request created the default pipeline. Use it.
            db.ChangeTracker.Clear();
            var raced = await db.Pipelines.FirstAsync(p => p.CompanyId == companyId && p.IsDefault);
            return (await GetPipelineAsync(companyId, raced.Id))!;
        }

        return (await GetPipelineAsync(companyId, pipeline.Id))!;
    }

    public async Task<IEnumerable<PipelineDto>> GetPipelinesAsync(int companyId)
    {
        return await db.Pipelines
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PipelineDto(
                p.Id, p.Name, p.IsDefault, p.SortOrder,
                p.Stages.OrderBy(s => s.SortOrder)
                    .Select(s => new StageDto(s.Id, s.PipelineId, s.Name, s.SortOrder, s.IsWon, s.IsLost))
                    .ToList()))
            .ToListAsync();
    }

    public async Task<PipelineDto?> GetPipelineAsync(int companyId, int pipelineId)
    {
        return await db.Pipelines
            .Where(p => p.CompanyId == companyId && p.Id == pipelineId)
            .Select(p => new PipelineDto(
                p.Id, p.Name, p.IsDefault, p.SortOrder,
                p.Stages.OrderBy(s => s.SortOrder)
                    .Select(s => new StageDto(s.Id, s.PipelineId, s.Name, s.SortOrder, s.IsWon, s.IsLost))
                    .ToList()))
            .FirstOrDefaultAsync();
    }

    public async Task<StageDto> AddStageAsync(int companyId, int pipelineId, string name, bool isWon, bool isLost)
    {
        var pipeline = await db.Pipelines.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Id == pipelineId)
            ?? throw new NotFoundException("Pipeline not found.");

        var maxOrder = await db.PipelineStages
            .Where(s => s.PipelineId == pipelineId)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync() ?? -1;

        var stage = new PipelineStage
        {
            CompanyId = companyId, PipelineId = pipeline.Id, Name = name.Trim(),
            SortOrder = maxOrder + 1, IsWon = isWon, IsLost = isLost,
        };
        db.PipelineStages.Add(stage);
        await db.SaveChangesAsync();

        return new StageDto(stage.Id, stage.PipelineId, stage.Name, stage.SortOrder, stage.IsWon, stage.IsLost);
    }

    public async Task<StageDto?> UpdateStageAsync(int companyId, int stageId, string name, bool isWon, bool isLost)
    {
        var stage = await db.PipelineStages.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Id == stageId);
        if (stage == null) return null;

        stage.Name = name.Trim();
        stage.IsWon = isWon;
        stage.IsLost = isLost;
        await db.SaveChangesAsync();

        return new StageDto(stage.Id, stage.PipelineId, stage.Name, stage.SortOrder, stage.IsWon, stage.IsLost);
    }

    public async Task RemoveStageAsync(int companyId, int stageId)
    {
        var stage = await db.PipelineStages.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Id == stageId)
            ?? throw new NotFoundException("Stage not found.");

        var hasDeals = await db.Deals.AnyAsync(d => d.StageId == stageId);
        if (hasDeals)
            throw new DomainException("This stage still has deals. Move them to another stage before deleting it.");

        db.PipelineStages.Remove(stage);
        await db.SaveChangesAsync();
    }

    public async Task ReorderStagesAsync(int companyId, int pipelineId, IList<int> orderedStageIds)
    {
        var stages = await db.PipelineStages
            .Where(s => s.CompanyId == companyId && s.PipelineId == pipelineId)
            .ToListAsync();

        var byId = stages.ToDictionary(s => s.Id);
        for (var i = 0; i < orderedStageIds.Count; i++)
            if (byId.TryGetValue(orderedStageIds[i], out var stage))
                stage.SortOrder = i;

        await db.SaveChangesAsync();
    }
}
