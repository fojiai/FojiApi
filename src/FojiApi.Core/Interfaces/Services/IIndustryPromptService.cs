using FojiApi.Core.Enums;

namespace FojiApi.Core.Interfaces.Services;

public interface IIndustryPromptService
{
    string GetSystemPrompt(IndustryType industryType, string companyName, AgentLanguage language);
}
