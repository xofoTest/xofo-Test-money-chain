using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

public class AdminService
{
    private readonly AppDbContext _db;
    private readonly PlacementService _placements;

    public AdminService(AppDbContext db, PlacementService placements)
    {
        _db = db;
        _placements = placements;
    }

    public async Task<AdminOverviewDto> OverviewAsync()
    {
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.Role == "member").ToListAsync();
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var regCount = await _db.RegistrationRecords.CountAsync();
        var ledger = await _db.RewardLedger.AsNoTracking().ToListAsync();
        var policy = await _db.RewardPolicies.OrderByDescending(p => p.PolicyVersion).FirstOrDefaultAsync();

        var childCount = placements.Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var fullNodes = profiles.Count(p => childCount.GetValueOrDefault(p.Id) >= 3);
        var openSlots = profiles.Sum(p => Math.Max(0, 3 - childCount.GetValueOrDefault(p.Id)));

        var recent = await RecentAuditAsync(10);

        return new AdminOverviewDto(
            profiles.Count,
            profiles.Count(p => p.AccountStatus == "active"),
            profiles.Count(p => p.AccountStatus == "suspended"),
            profiles.Count(p => p.AccountStatus == "pending"),
            regCount,
            profiles.Count,
            fullNodes,
            openSlots,
            ledger.Sum(l => l.AmountPaise),
            ledger.Where(l => l.Status == "pending").Sum(l => l.AmountPaise),
            ledger.Where(l => l.Status == "settled").Sum(l => l.AmountPaise),
            policy?.SimulationEnabled ?? false,
            recent);
    }

    public async Task<List<MemberRowDto>> MembersAsync(string? q, string? status)
    {
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.Role == "member").ToListAsync();
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var sponsorships = await _db.Sponsorships.AsNoTracking().ToListAsync();
        var sponsorById = sponsorships.ToDictionary(s => s.SponsoredId, s => s.SponsorId);
        var profileById = profiles.ToDictionary(p => p.Id, p => p);
        var childCounts = placements.Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = profiles.Select(p =>
        {
            string? sponsorCode = null, sponsorName = null;
            if (sponsorById.TryGetValue(p.Id, out var spId) && profileById.TryGetValue(spId, out var sp))
            {
                sponsorCode = sp.MemberCode;
                sponsorName = sp.FullName;
            }
            var placement = placements.FirstOrDefault(pl => pl.MemberId == p.Id);
            Profile? parentProfile = null;
            if (placement?.ParentId is Guid pp && profileById.TryGetValue(pp, out var parent))
                parentProfile = parent;
            return new MemberRowDto(
                p.MemberCode, p.FullName, p.Email, p.Phone, p.AccountStatus,
                sponsorCode ?? "—", sponsorName ?? "—",
                parentProfile?.MemberCode, parentProfile?.FullName,
                p.PreferredLanguage, p.CreatedAt.ToString("dd MMM yyyy"),
                childCounts.GetValueOrDefault(p.Id));
        });

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            rows = rows.Where(m => m.Status == status);
        var needle = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(needle))
            rows = rows.Where(m =>
                m.MemberCode.ToLowerInvariant().Contains(needle) ||
                m.Name.ToLowerInvariant().Contains(needle) ||
                m.Email.ToLowerInvariant().Contains(needle) ||
                (m.Phone ?? "").ToLowerInvariant().Contains(needle));

        return rows.OrderBy(m => m.MemberCode).ToList();
    }

    public async Task<Profile> ChangeStatusAsync(Guid adminId, string memberCode, StatusChangeRequest req)
    {
        if (req.Status is not ("active" or "suspended" or "pending"))
            throw new DomainException("Invalid status.");
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new DomainException("A reason is required for status changes.");

        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.MemberCode == memberCode && p.Role == "member")
            ?? throw new DomainException("Member not found", 404);

        profile.AccountStatus = req.Status;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminId = adminId,
            Action = $"account_{req.Status}",
            TargetType = "member",
            TargetId = memberCode,
            Reason = req.Reason,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task<List<SlotRowDto>> SlotsAsync()
    {
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.Role == "member").ToListAsync();
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var counts = placements.Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        return profiles.Select(p => new SlotRowDto(
            p.MemberCode, p.FullName, counts.GetValueOrDefault(p.Id),
            Math.Max(0, 3 - counts.GetValueOrDefault(p.Id)), p.AccountStatus))
            .OrderBy(s => s.MemberCode).ToList();
    }

    public async Task<List<PlacementHistoryDto>> PlacementHistoryAsync()
    {
        var logs = (await _db.AdminAuditLogs.AsNoTracking()
            .Where(a => a.Action.StartsWith("placement_"))
            .ToListAsync())
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToList();
        return logs.Select(l => new PlacementHistoryDto(
            l.CreatedAt.ToString("dd MMM yyyy, HH:mm"),
            l.TargetId,
            l.Action == "placement_created" ? $"Placed as new record ({l.Reason})" : $"Re-parented ({l.Reason})",
            l.Admin?.FullName ?? "Admin",
            l.Reason)).ToList();
    }

    public async Task<List<string>> ValidateTreeAsync() => await _placements.ValidateTreeAsync();

    public async Task CorrectPlacementAsync(Guid adminId, PlacementCorrectionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
            throw new DomainException("A reason is required for placement corrections.");
        var member = await _db.Profiles.FirstOrDefaultAsync(p => p.MemberCode == req.MemberCode && p.Role == "member")
            ?? throw new DomainException("Member not found", 404);
        Guid? parentId = null;
        if (!string.IsNullOrWhiteSpace(req.NewParentCode))
        {
            var parent = await _db.Profiles.FirstOrDefaultAsync(p => p.MemberCode == req.NewParentCode && p.Role == "member")
                ?? throw new DomainException("New parent not found", 404);
            parentId = parent.Id;
        }
        await _placements.PlaceMemberAsync(member.Id, parentId, req.Reason, adminId, isCorrection: true);
    }

    public async Task<List<LedgerRowDto>> TransactionsAsync(string? type, string? status, string? q)
    {
        var regs = await _db.RegistrationRecords.AsNoTracking().Include(r => r.Member).ToListAsync();
        var ledger = await _db.RewardLedger.AsNoTracking().Include(l => l.Member).ToListAsync();

        var rows = new List<LedgerRowDto>();
        rows.AddRange(regs.Select(r => new LedgerRowDto(
            r.ReferenceCode, r.Member.MemberCode, r.Member.FullName, "registration", r.AmountPaise,
            r.Status, r.IsSimulated, r.CreatedAt.ToString("dd MMM yyyy"), r.ReferenceCode, "Registration entry")));
        rows.AddRange(ledger.Select(l => new LedgerRowDto(
            l.Id.ToString(), l.Member.MemberCode, l.Member.FullName, l.RewardType, l.AmountPaise,
            l.Status, l.IsSimulated, l.CreatedAt.ToString("dd MMM yyyy"),
            l.TriggerEventId ?? l.Id.ToString(), l.Description)));

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
            rows = rows.Where(r => r.Type == type).ToList();
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            rows = rows.Where(r => r.Status == status).ToList();
        var needle = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(needle))
            rows = rows.Where(r =>
                r.MemberCode.ToLowerInvariant().Contains(needle) ||
                r.MemberName.ToLowerInvariant().Contains(needle) ||
                r.Reference.ToLowerInvariant().Contains(needle)).ToList();

        return rows.OrderByDescending(r => r.Date).ToList();
    }

    public async Task<SettingsDto> GetSettingsAsync()
    {
        var policy = await _db.RewardPolicies.OrderByDescending(p => p.PolicyVersion).FirstOrDefaultAsync()
            ?? new RewardPolicy();
        var settings = await _db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
        return new SettingsDto(
            Get(settings, "default_language", "en"),
            int.TryParse(Get(settings, "registration_fee_inr", "50"), out var fee) ? fee : 50,
            policy.DirectMemberCountThreshold,
            policy.DirectMemberRewardPaise / 100,
            policy.ChildRewardPaise / 100,
            policy.ChildRewardTrigger,
            policy.SimulationEnabled,
            false, // live payments hard-disabled
            false, // live payouts hard-disabled
            Get(settings, "maintenance_mode", "false") == "true");
    }

    public async Task<SettingsDto> UpdateSettingsAsync(Guid adminId, SettingsUpdateRequest req)
    {
        var policy = await _db.RewardPolicies.OrderByDescending(p => p.PolicyVersion).FirstOrDefaultAsync();
        if (policy is null)
        {
            policy = new RewardPolicy();
            _db.RewardPolicies.Add(policy);
        }

        policy.DirectMemberCountThreshold = req.DirectMemberCountThreshold is >= 1 and <= 100 ? req.DirectMemberCountThreshold.Value : policy.DirectMemberCountThreshold;
        policy.DirectMemberRewardPaise = InrToPaise(req.DirectMemberRewardInr, policy.DirectMemberRewardPaise);
        policy.ChildRewardPaise = InrToPaise(req.ChildRewardInr, policy.ChildRewardPaise);
        if (req.ChildRewardTrigger is "child_completes_three_placements")
            policy.ChildRewardTrigger = req.ChildRewardTrigger;
        if (req.RewardSimulationEnabled is bool sim)
            policy.SimulationEnabled = sim;
        policy.UpdatedAt = DateTimeOffset.UtcNow;

        await SetSettingAsync("default_language", req.DefaultLanguage is "en" or "ml" ? req.DefaultLanguage : null);
        if (req.RegistrationFeeInr is >= 1 and <= 100000)
            await SetSettingAsync("registration_fee_inr", req.RegistrationFeeInr.Value.ToString());
        if (req.MaintenanceMode is bool mm)
            await SetSettingAsync("maintenance_mode", mm ? "true" : "false");

        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminId = adminId,
            Action = "settings_updated",
            TargetType = "settings",
            TargetId = "reward_policy",
            Reason = "Settings changed by administrator",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                simulationEnabled = policy.SimulationEnabled,
                livePayments = false,
                livePayouts = false
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
        return await GetSettingsAsync();
    }

    private async Task SetSettingAsync(string key, string? value)
    {
        if (value is null) return;
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static int InrToPaise(int? inr, int fallback) => inr is >= 0 and <= 100000 ? inr.Value * 100 : fallback;
    private static string Get(Dictionary<string, string> s, string key, string fallback) =>
        s.TryGetValue(key, out var v) ? v : fallback;

    public async Task<List<AuditRowDto>> RecentAuditAsync(int take = 10)
    {
        var logs = (await _db.AdminAuditLogs.AsNoTracking().Include(a => a.Admin).ToListAsync())
            .OrderByDescending(a => a.CreatedAt).Take(take).ToList();
        return logs.Select(l => new AuditRowDto(
            l.CreatedAt.ToString("dd MMM yyyy, HH:mm"),
            l.Admin?.FullName ?? "System",
            l.Action,
            l.TargetId,
            l.Reason)).ToList();
    }
}
