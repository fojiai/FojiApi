namespace FojiApi.Core.Interfaces.Services;

public interface IDealService
{
    Task<BoardDto> GetBoardAsync(int companyId, int? pipelineId = null);
    Task<DealDto> CreateDealAsync(int companyId, CreateDealInput input);
    Task<DealDto?> UpdateDealAsync(int companyId, int dealId, UpdateDealInput input);
    Task<DealDto?> MoveStageAsync(int companyId, int dealId, int toStageId, int? changedByUserId);
}

public record BoardDto(int PipelineId, string PipelineName, IReadOnlyList<BoardColumn> Columns);

public record BoardColumn(StageDto Stage, decimal Total, IReadOnlyList<DealCard> Deals);

public record DealCard(
    int Id,
    string Title,
    decimal Value,
    string Currency,
    string Status,
    int ContactId,
    string? ContactName,
    int? OwnerUserId,
    string? OwnerName,
    DateTime? ExpectedCloseDate
);

public record DealDto(
    int Id,
    int PipelineId,
    int StageId,
    string StageName,
    int ContactId,
    string? ContactName,
    int? OwnerUserId,
    string? OwnerName,
    string Title,
    decimal Value,
    string Currency,
    string Status,
    DateTime? ExpectedCloseDate,
    DateTime? ClosedAt,
    DateTime CreatedAt
);

public record CreateDealInput(
    int? PipelineId,
    int? StageId,
    int ContactId,
    int? OwnerUserId,
    string Title,
    decimal Value,
    string? Currency,
    DateTime? ExpectedCloseDate
);

public record UpdateDealInput(
    int? OwnerUserId,
    string Title,
    decimal Value,
    string? Currency,
    DateTime? ExpectedCloseDate
);
