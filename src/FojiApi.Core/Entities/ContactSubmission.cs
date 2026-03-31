namespace FojiApi.Core.Entities;

public class ContactSubmission : BaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsResolved { get; set; } = false;
    public string? AdminNotes { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
