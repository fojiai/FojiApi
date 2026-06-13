using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FojiApi.Infrastructure.Services;

public class ContactService(FojiDbContext db) : IContactService
{
    // ── Capture + dedup/upsert (internal) ─────────────────────────────────────

    public async Task<ContactCaptureResult> CaptureLeadAndUpsertContactAsync(
        int agentId, string sessionId, string? name, string? email, string? phone, string source)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new NotFoundException("Agent not found.");
        var companyId = agent.CompanyId;

        var emailNorm = NormalizeEmail(email);
        var phoneNorm = NormalizePhone(phone);

        // Find-or-create the deduped contact first (own save + race retry), so the lead links cleanly.
        Contact? contact = null;
        if (emailNorm != null || phoneNorm != null)
            contact = await UpsertContactAsync(companyId, name, email, phone, emailNorm, phoneNorm, source);

        var lead = new Lead
        {
            AgentId = agentId,
            CompanyId = companyId,
            Name = Trim(name),
            Email = Trim(email),
            Phone = Trim(phone),
            SessionId = sessionId,
            Source = source,
            ContactId = contact?.Id,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        return new ContactCaptureResult(lead.Id, contact?.Id, sessionId);
    }

    private async Task<Contact> UpsertContactAsync(
        int companyId, string? name, string? email, string? phone, string? emailNorm, string? phoneNorm, string source)
    {
        var byEmail = emailNorm == null ? null :
            await db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.EmailNormalized == emailNorm);
        var byPhone = phoneNorm == null ? null :
            await db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.PhoneNormalized == phoneNorm);

        if (byEmail == null && byPhone == null)
            return await InsertNewContactAsync(companyId, name, email, phone, emailNorm, phoneNorm, source);

        Contact contact;
        var conflict = byEmail != null && byPhone != null && byEmail.Id != byPhone.Id;
        if (conflict)
        {
            // Identity collision (email → X, phone → Y): oldest wins, flag for human merge, don't move identifiers.
            contact = byEmail!.CreatedAt <= byPhone!.CreatedAt ? byEmail : byPhone;
            contact.NeedsReviewDuplicate = true;
        }
        else
        {
            contact = byEmail ?? byPhone!;
            // Best-effort backfill of the missing identifier (skip if another contact already owns it).
            if (contact.EmailNormalized == null && emailNorm != null)
            {
                var taken = await db.Contacts.AnyAsync(c =>
                    c.CompanyId == companyId && c.EmailNormalized == emailNorm && c.Id != contact.Id);
                if (taken) contact.NeedsReviewDuplicate = true;
                else { contact.Email = Trim(email); contact.EmailNormalized = emailNorm; }
            }
            if (contact.PhoneNormalized == null && phoneNorm != null)
            {
                var taken = await db.Contacts.AnyAsync(c =>
                    c.CompanyId == companyId && c.PhoneNormalized == phoneNorm && c.Id != contact.Id);
                if (taken) contact.NeedsReviewDuplicate = true;
                else { contact.Phone = Trim(phone); contact.PhoneNormalized = phoneNorm; }
            }
        }

        // Backfill name only if empty; always bump activity.
        if (string.IsNullOrWhiteSpace(contact.Name) && !string.IsNullOrWhiteSpace(name))
            contact.Name = name!.Trim();
        contact.LastActivityAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Rare simultaneous backfill — discard local changes, keep the contact as it is in the DB.
            await db.Entry(contact).ReloadAsync();
        }
        return contact;
    }

    private async Task<Contact> InsertNewContactAsync(
        int companyId, string? name, string? email, string? phone, string? emailNorm, string? phoneNorm, string source)
    {
        var contact = new Contact
        {
            CompanyId = companyId,
            Name = Trim(name),
            Email = Trim(email),
            Phone = Trim(phone),
            EmailNormalized = emailNorm,
            PhoneNormalized = phoneNorm,
            Status = ContactStatus.New,
            Source = source,
            LastActivityAt = DateTime.UtcNow,
        };
        db.Contacts.Add(contact);
        try
        {
            await db.SaveChangesAsync();
            return contact;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the create race — another request just inserted this contact. Re-select and use it.
            db.Entry(contact).State = EntityState.Detached;
            var existing = await db.Contacts.FirstOrDefaultAsync(c =>
                c.CompanyId == companyId &&
                ((emailNorm != null && c.EmailNormalized == emailNorm) ||
                 (phoneNorm != null && c.PhoneNormalized == phoneNorm)));
            return existing ?? throw new ConflictException("Could not resolve contact after a concurrent insert.");
        }
    }

    // ── Dashboard reads ───────────────────────────────────────────────────────

    public async Task<IEnumerable<ContactListItem>> GetContactsAsync(
        int companyId, int? ownerUserId = null, ContactStatus? status = null, string? tag = null, string? search = null)
    {
        var query = db.Contacts.Where(c => c.CompanyId == companyId);

        if (ownerUserId.HasValue) query = query.Where(c => c.OwnerUserId == ownerUserId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var t = tag.Trim().ToLowerInvariant();
            query = query.Where(c => c.Tags.Any(x => x.Tag == t));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)) ||
                (c.Phone != null && c.Phone.Contains(s)));
        }

        return await query
            .OrderByDescending(c => c.LastActivityAt ?? c.CreatedAt)
            .Select(c => new ContactListItem(
                c.Id, c.Name, c.Email, c.Phone, c.Status.ToString(), c.Source,
                c.OwnerUserId,
                c.OwnerUser != null ? (c.OwnerUser.FirstName + " " + c.OwnerUser.LastName).Trim() : null,
                c.EstimatedValue, c.NeedsReviewDuplicate, c.LastActivityAt,
                c.Tags.Select(x => x.Tag).ToList(),
                c.Leads.Count, c.Deals.Count, c.CreatedAt))
            .ToListAsync();
    }

    public async Task<ContactDetail?> GetContactAsync(int companyId, int contactId)
    {
        return await db.Contacts
            .Where(c => c.CompanyId == companyId && c.Id == contactId)
            .Select(c => new ContactDetail(
                c.Id, c.Name, c.Email, c.Phone, c.Status.ToString(), c.Source,
                c.OwnerUserId,
                c.OwnerUser != null ? (c.OwnerUser.FirstName + " " + c.OwnerUser.LastName).Trim() : null,
                c.EstimatedValue, c.Notes, c.NeedsReviewDuplicate, c.LastActivityAt,
                c.Tags.Select(x => x.Tag).ToList(), c.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TimelineItem>> GetTimelineAsync(int companyId, int contactId)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == contactId)
            ?? throw new NotFoundException("Contact not found.");

        var items = new List<TimelineItem>();

        // Lead capture events (and the session ids used to find related handoffs).
        var leads = await db.Leads
            .Where(l => l.ContactId == contactId)
            .Select(l => new { l.Id, l.Source, l.SessionId, l.CreatedAt })
            .ToListAsync();
        foreach (var l in leads)
            items.Add(new TimelineItem("lead", l.CreatedAt, "Lead captured", l.Source, l.Id));

        var sessionIds = leads.Select(l => l.SessionId).Distinct().ToList();
        if (sessionIds.Count > 0)
        {
            var handoffs = await db.HandoffEvents
                .Where(h => h.CompanyId == companyId && sessionIds.Contains(h.SessionId))
                .Select(h => new { h.Id, h.UserMessage, h.CreatedAt })
                .ToListAsync();
            foreach (var h in handoffs)
                items.Add(new TimelineItem("handoff", h.CreatedAt, "Requested human handoff", h.UserMessage, h.Id));
        }

        // Deals + stage history.
        var deals = await db.Deals
            .Where(d => d.ContactId == contactId)
            .Select(d => new { d.Id, d.Title, d.CreatedAt })
            .ToListAsync();
        foreach (var d in deals)
            items.Add(new TimelineItem("deal_created", d.CreatedAt, "Deal created", d.Title, d.Id));

        var dealIds = deals.Select(d => d.Id).ToList();
        if (dealIds.Count > 0)
        {
            var history = await db.DealStageHistory
                .Where(h => dealIds.Contains(h.DealId))
                .Join(db.PipelineStages, h => h.ToStageId, s => s.Id, (h, s) => new { h.DealId, h.CreatedAt, StageName = s.Name, s.IsWon, s.IsLost })
                .ToListAsync();
            foreach (var h in history)
            {
                var type = h.IsWon ? "deal_won" : h.IsLost ? "deal_lost" : "deal_stage";
                items.Add(new TimelineItem(type, h.CreatedAt, $"Moved to {h.StageName}", h.StageName, h.DealId));
            }
        }

        return items.OrderByDescending(i => i.Timestamp).ToList();
    }

    // ── Dashboard writes ──────────────────────────────────────────────────────

    public async Task<ContactDetail> CreateContactAsync(int companyId, ContactInput input)
    {
        await ValidateOwnerAsync(companyId, input.OwnerUserId);

        var emailNorm = NormalizeEmail(input.Email);
        var phoneNorm = NormalizePhone(input.Phone);

        var contact = new Contact
        {
            CompanyId = companyId,
            Name = Trim(input.Name),
            Email = Trim(input.Email),
            Phone = Trim(input.Phone),
            EmailNormalized = emailNorm,
            PhoneNormalized = phoneNorm,
            OwnerUserId = input.OwnerUserId,
            Status = input.Status,
            Source = string.IsNullOrWhiteSpace(input.Source) ? "manual" : input.Source,
            EstimatedValue = input.EstimatedValue,
            Notes = input.Notes,
            LastActivityAt = DateTime.UtcNow,
        };
        db.Contacts.Add(contact);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("A contact with this email or phone already exists.");
        }

        return (await GetContactAsync(companyId, contact.Id))!;
    }

    public async Task<ContactDetail?> UpdateContactAsync(int companyId, int contactId, ContactInput input)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == contactId);
        if (contact == null) return null;

        await ValidateOwnerAsync(companyId, input.OwnerUserId);

        contact.Name = Trim(input.Name);
        contact.Email = Trim(input.Email);
        contact.Phone = Trim(input.Phone);
        contact.EmailNormalized = NormalizeEmail(input.Email);
        contact.PhoneNormalized = NormalizePhone(input.Phone);
        contact.OwnerUserId = input.OwnerUserId;
        contact.Status = input.Status;
        if (!string.IsNullOrWhiteSpace(input.Source)) contact.Source = input.Source;
        contact.EstimatedValue = input.EstimatedValue;
        contact.Notes = input.Notes;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("A contact with this email or phone already exists.");
        }

        return await GetContactAsync(companyId, contactId);
    }

    public async Task<IReadOnlyList<string>> AddTagAsync(int companyId, int contactId, string tag)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == contactId)
            ?? throw new NotFoundException("Contact not found.");

        var t = tag.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t)) throw new DomainException("Tag cannot be empty.");

        var exists = await db.ContactTags.AnyAsync(x => x.ContactId == contactId && x.Tag == t);
        if (!exists)
        {
            db.ContactTags.Add(new ContactTag { ContactId = contactId, CompanyId = companyId, Tag = t });
            await db.SaveChangesAsync();
        }

        return await db.ContactTags.Where(x => x.ContactId == contactId).Select(x => x.Tag).ToListAsync();
    }

    public async Task<IReadOnlyList<string>> RemoveTagAsync(int companyId, int contactId, string tag)
    {
        var t = tag.Trim().ToLowerInvariant();
        var existing = await db.ContactTags.FirstOrDefaultAsync(x =>
            x.CompanyId == companyId && x.ContactId == contactId && x.Tag == t);
        if (existing != null)
        {
            db.ContactTags.Remove(existing);
            await db.SaveChangesAsync();
        }

        return await db.ContactTags.Where(x => x.ContactId == contactId).Select(x => x.Tag).ToListAsync();
    }

    public Task<int> GetContactCountAsync(int companyId) =>
        db.Contacts.CountAsync(c => c.CompanyId == companyId);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ValidateOwnerAsync(int companyId, int? ownerUserId)
    {
        if (ownerUserId == null) return;
        var isMember = await db.UserCompanies.AnyAsync(uc =>
            uc.CompanyId == companyId && uc.UserId == ownerUserId.Value && uc.IsActive);
        if (!isMember)
            throw new DomainException("The selected owner is not an active member of this company.");
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        // Default Brazilian country code for local 10/11-digit numbers (market is Brazil).
        if (digits.Length is 10 or 11) digits = "55" + digits;
        return digits;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" };
}
