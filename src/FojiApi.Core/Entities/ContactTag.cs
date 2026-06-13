namespace FojiApi.Core.Entities;

/// <summary>A free-form tag on a CRM contact (e.g. "MBA", "Indicação"). Stored lowercased.</summary>
public class ContactTag : BaseEntity
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public int CompanyId { get; set; }
    public string Tag { get; set; } = string.Empty;

    // Navigation
    public Contact Contact { get; set; } = null!;
}
