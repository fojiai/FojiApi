namespace FojiApi.Core.Interfaces.Services;

public interface IPipelineService
{
    /// <summary>Lazily creates the company's default "Sales" pipeline + stages on first CRM use.</summary>
    Task<PipelineDto> EnsureDefaultPipelineAsync(int companyId);

    Task<IEnumerable<PipelineDto>> GetPipelinesAsync(int companyId);
    Task<PipelineDto?> GetPipelineAsync(int companyId, int pipelineId);
    Task<StageDto> AddStageAsync(int companyId, int pipelineId, string name, bool isWon, bool isLost);
    Task<StageDto?> UpdateStageAsync(int companyId, int stageId, string name, bool isWon, bool isLost);
    Task RemoveStageAsync(int companyId, int stageId);
    Task ReorderStagesAsync(int companyId, int pipelineId, IList<int> orderedStageIds);
}

public record PipelineDto(int Id, string Name, bool IsDefault, int SortOrder, IReadOnlyList<StageDto> Stages);

public record StageDto(int Id, int PipelineId, string Name, int SortOrder, bool IsWon, bool IsLost);
