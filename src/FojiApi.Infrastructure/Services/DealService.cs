using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class DealService(FojiDbContext db, IPipelineService pipelineService) : IDealService
{
    public async Task<BoardDto> GetBoardAsync(int companyId, int? pipelineId = null)
    {
        // Resolve the pipeline (default, seeded on first use, if none specified).
        var pipeline = pipelineId.HasValue
            ? await pipelineService.GetPipelineAsync(companyId, pipelineId.Value)
              ?? throw new NotFoundException("Pipeline not found.")
            : await pipelineService.EnsureDefaultPipelineAsync(companyId);

        var columns = new List<BoardColumn>();
        var dealsByStage = await db.Deals
            .Where(d => d.CompanyId == companyId && d.PipelineId == pipeline.Id)
            .GroupBy(d => d.StageId)
            .Select(g => new { StageId = g.Key, Total = g.Sum(x => x.Value) })
            .ToDictionaryAsync(x => x.StageId, x => x.Total);

        // Cards grouped per stage (in memory).
        var cardsByStage = (await db.Deals
            .Where(d => d.CompanyId == companyId && d.PipelineId == pipeline.Id)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.StageId,
                Card = new DealCard(
                    d.Id, d.Title, d.Value, d.Currency, d.Status.ToString(),
                    d.ContactId, d.Contact.Name,
                    d.OwnerUserId,
                    d.OwnerUser != null ? (d.OwnerUser.FirstName + " " + d.OwnerUser.LastName).Trim() : null,
                    d.ExpectedCloseDate)
            })
            .ToListAsync())
            .GroupBy(x => x.StageId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Card).ToList());

        foreach (var stage in pipeline.Stages)
        {
            var stageCards = cardsByStage.TryGetValue(stage.Id, out var c) ? c : [];
            var total = dealsByStage.TryGetValue(stage.Id, out var t) ? t : 0m;
            columns.Add(new BoardColumn(stage, total, stageCards));
        }

        return new BoardDto(pipeline.Id, pipeline.Name, columns);
    }

    public async Task<DealDto> CreateDealAsync(int companyId, CreateDealInput input, int? actingUserId = null)
    {
        var pipeline = input.PipelineId.HasValue
            ? await pipelineService.GetPipelineAsync(companyId, input.PipelineId.Value)
              ?? throw new NotFoundException("Pipeline not found.")
            : await pipelineService.EnsureDefaultPipelineAsync(companyId);

        if (pipeline.Stages.Count == 0)
            throw new DomainException("This pipeline has no stages.");

        var stage = input.StageId.HasValue
            ? pipeline.Stages.FirstOrDefault(s => s.Id == input.StageId.Value)
              ?? throw new NotFoundException("Stage not found in this pipeline.")
            : pipeline.Stages.OrderBy(s => s.SortOrder).First();

        var contactExists = await db.Contacts.AnyAsync(c => c.CompanyId == companyId && c.Id == input.ContactId);
        if (!contactExists) throw new NotFoundException("Contact not found.");

        await ValidateOwnerAsync(companyId, input.OwnerUserId);

        var deal = new Deal
        {
            CompanyId = companyId,
            PipelineId = pipeline.Id,
            StageId = stage.Id,
            ContactId = input.ContactId,
            OwnerUserId = input.OwnerUserId,
            Title = input.Title.Trim(),
            Value = input.Value,
            Currency = string.IsNullOrWhiteSpace(input.Currency) ? "BRL" : input.Currency!.Trim().ToUpperInvariant(),
            ExpectedCloseDate = input.ExpectedCloseDate,
        };
        ApplyStageStatus(deal, stage);
        db.Deals.Add(deal);

        db.DealStageHistory.Add(new DealStageHistory
        {
            CompanyId = companyId, Deal = deal, FromStageId = null, ToStageId = stage.Id,
            // The acting user, not the deal owner — these are different people.
            ChangedByUserId = actingUserId,
        });

        // Bump the contact's activity.
        var contact = await db.Contacts.FirstAsync(c => c.Id == input.ContactId);
        contact.LastActivityAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (await GetDealAsync(companyId, deal.Id))!;
    }

    public async Task<DealDto?> UpdateDealAsync(int companyId, int dealId, UpdateDealInput input)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Id == dealId);
        if (deal == null) return null;

        await ValidateOwnerAsync(companyId, input.OwnerUserId);

        deal.OwnerUserId = input.OwnerUserId;
        deal.Title = input.Title.Trim();
        deal.Value = input.Value;
        if (!string.IsNullOrWhiteSpace(input.Currency)) deal.Currency = input.Currency!.Trim().ToUpperInvariant();
        deal.ExpectedCloseDate = input.ExpectedCloseDate;

        await db.SaveChangesAsync();
        return await GetDealAsync(companyId, dealId);
    }

    public async Task<DealDto?> MoveStageAsync(int companyId, int dealId, int toStageId, int? changedByUserId)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Id == dealId);
        if (deal == null) return null;

        if (deal.StageId == toStageId)
            return await GetDealAsync(companyId, dealId);

        var target = await db.PipelineStages.FirstOrDefaultAsync(s =>
            s.CompanyId == companyId && s.Id == toStageId && s.PipelineId == deal.PipelineId)
            ?? throw new NotFoundException("Target stage not found in this deal's pipeline.");

        var fromStageId = deal.StageId;
        deal.StageId = target.Id;
        ApplyStageStatus(deal, new StageDto(target.Id, target.PipelineId, target.Name, target.SortOrder, target.IsWon, target.IsLost));

        db.DealStageHistory.Add(new DealStageHistory
        {
            CompanyId = companyId, DealId = deal.Id, FromStageId = fromStageId, ToStageId = target.Id,
            ChangedByUserId = changedByUserId,
        });

        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == deal.ContactId);
        if (contact != null) contact.LastActivityAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetDealAsync(companyId, dealId);
    }

    public async Task<bool> DeleteDealAsync(int companyId, int dealId)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Id == dealId);
        if (deal == null) return false;

        // Stage history has no cascade from Deal, so clear it explicitly.
        var history = await db.DealStageHistory
            .Where(h => h.CompanyId == companyId && h.DealId == dealId)
            .ToListAsync();
        db.DealStageHistory.RemoveRange(history);

        // Tasks, meetings and emails reference the deal optionally — unlink rather
        // than delete, so the activity record survives on the contact.
        var tasks = await db.CrmTasks.Where(x => x.CompanyId == companyId && x.DealId == dealId).ToListAsync();
        foreach (var x in tasks) x.DealId = null;
        var meetings = await db.Meetings.Where(x => x.CompanyId == companyId && x.DealId == dealId).ToListAsync();
        foreach (var x in meetings) x.DealId = null;
        var emails = await db.EmailLogs.Where(x => x.CompanyId == companyId && x.DealId == dealId).ToListAsync();
        foreach (var x in emails) x.DealId = null;

        db.Deals.Remove(deal);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public async Task<DealDto?> GetDealAsync(int companyId, int dealId)
    {
        return await db.Deals
            .Where(d => d.CompanyId == companyId && d.Id == dealId)
            .Select(d => new DealDto(
                d.Id, d.PipelineId, d.StageId, d.Stage.Name,
                d.ContactId, d.Contact.Name,
                d.OwnerUserId,
                d.OwnerUser != null ? (d.OwnerUser.FirstName + " " + d.OwnerUser.LastName).Trim() : null,
                d.Title, d.Value, d.Currency, d.Status.ToString(),
                d.ExpectedCloseDate, d.ClosedAt, d.CreatedAt))
            .FirstOrDefaultAsync();
    }

    private static void ApplyStageStatus(Deal deal, StageDto stage)
    {
        if (stage.IsWon)
        {
            deal.Status = DealStatus.Won;
            deal.ClosedAt ??= DateTime.UtcNow;
        }
        else if (stage.IsLost)
        {
            deal.Status = DealStatus.Lost;
            deal.ClosedAt ??= DateTime.UtcNow;
        }
        else
        {
            deal.Status = DealStatus.Open;
            deal.ClosedAt = null;
        }
    }

    private async Task ValidateOwnerAsync(int companyId, int? ownerUserId)
    {
        if (ownerUserId == null) return;
        var isMember = await db.UserCompanies.AnyAsync(uc =>
            uc.CompanyId == companyId && uc.UserId == ownerUserId.Value && uc.IsActive);
        if (!isMember)
            throw new DomainException("The selected owner is not an active member of this company.");
    }
}
