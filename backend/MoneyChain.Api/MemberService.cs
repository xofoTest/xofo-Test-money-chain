using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

public class MemberService
{
    private readonly AppDbContext _db;
    private readonly SecurityService _security;
    private readonly PlacementService _placements;

    public MemberService(AppDbContext db, SecurityService security, PlacementService placements)
    {
        _db = db;
        _security = security;
        _placements = placements;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");
    }

    private static string NormalizeEmail(string email) =>
        (email ?? "").Trim().ToLowerInvariant();

    public async Task<(Profile profile, string token)> RegisterAsync(RegisterRequest req, Guid? actorId = null)
    {
        if (!req.TermsAccepted)
            throw new DomainException("You must accept the demo terms.");
        if (string.IsNullOrWhiteSpace(req.FullName))
            throw new DomainException("Full name is required.");
        var email = NormalizeEmail(req.Email);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email address is required.");
        ValidatePassword(req.Password);
        if (req.PreferredLanguage is not ("en" or "ml"))
            throw new DomainException("Preferred language must be 'en' or 'ml'.");

        if (await _db.Profiles.AnyAsync(p => p.Email == email))
            throw new DomainException("An account with this email already exists.");

        var sponsor = await _db.Profiles.FirstOrDefaultAsync(p => p.MemberCode == req.SponsorCode && p.Role == "member")
            ?? throw new DomainException("Sponsor code not found.");

        var now = DateTimeOffset.UtcNow;
        var profile = new Profile
        {
            MemberCode = await NextMemberCodeAsync(),
            FullName = req.FullName.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            PasswordHash = _security.HashPassword(req.Password),
            PreferredLanguage = req.PreferredLanguage,
            Role = "member",
            AccountStatus = "pending",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Profiles.Add(profile);

        _db.Sponsorships.Add(new Sponsorship
        {
            SponsorId = sponsor.Id,
            Sponsored = profile,
            CreatedAt = now
        });

        // Self-registration places the new member under their sponsor by default.
        await _placements.PlaceMemberAsync(profile.Id, sponsor.Id, "Self-registration", actorId ?? sponsor.Id, isCorrection: false);

        _db.RegistrationRecords.Add(new RegistrationRecord
        {
            Member = profile,
            AmountPaise = await RegistrationFeePaiseAsync(),
            Status = "simulated",
            IsSimulated = true,
            ReferenceCode = await NextReferenceAsync(),
            CreatedAt = now
        });

        await _db.SaveChangesAsync();
        return (profile, _security.CreateToken(profile));
    }

    public async Task<(Profile profile, string token)> AddMemberAsync(AddMemberRequest req, Profile sponsor)
    {
        if (!req.TermsAccepted)
            throw new DomainException("You must confirm this is a demo record.");
        if (string.IsNullOrWhiteSpace(req.FullName))
            throw new DomainException("Full name is required.");
        var email = NormalizeEmail(req.Email);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid email address is required.");
        if (req.PreferredLanguage is not ("en" or "ml"))
            throw new DomainException("Preferred language must be 'en' or 'ml'.");
        if (await _db.Profiles.AnyAsync(p => p.Email == email))
            throw new DomainException("An account with this email already exists.");

        var parent = await _db.Profiles.FirstOrDefaultAsync(p => p.MemberCode == req.PlacementParentCode && p.Role == "member")
            ?? throw new DomainException("Placement parent not found.");

        var now = DateTimeOffset.UtcNow;
        var profile = new Profile
        {
            MemberCode = await NextMemberCodeAsync(),
            FullName = req.FullName.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            // Added members receive a random temporary credential; only the demo admin can reset it.
            PasswordHash = _security.HashPassword(Guid.NewGuid().ToString("N")),
            PreferredLanguage = req.PreferredLanguage,
            Role = "member",
            AccountStatus = "pending",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Profiles.Add(profile);

        _db.Sponsorships.Add(new Sponsorship { SponsorId = sponsor.Id, Sponsored = profile, CreatedAt = now });
        await _placements.PlaceMemberAsync(profile.Id, parent.Id, $"Added by {sponsor.MemberCode}", sponsor.Id, isCorrection: false);

        _db.RegistrationRecords.Add(new RegistrationRecord
        {
            Member = profile,
            AmountPaise = await RegistrationFeePaiseAsync(),
            Status = "simulated",
            IsSimulated = true,
            ReferenceCode = await NextReferenceAsync(),
            CreatedAt = now
        });

        await _db.SaveChangesAsync();
        return (profile, _security.CreateToken(profile));
    }

    public async Task<(Profile profile, string token)> LoginAsync(LoginRequest req)
    {
        var email = NormalizeEmail(req.Email);
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Email == email)
            ?? throw new DomainException("Invalid email or password.", 401);

        if (!_security.VerifyPassword(profile.PasswordHash, req.Password ?? ""))
            throw new DomainException("Invalid email or password.", 401);

        if (profile.AccountStatus == "suspended")
            throw new DomainException("This account is suspended. Contact an administrator.", 403);

        return (profile, _security.CreateToken(profile));
    }

    public async Task<Profile> GetProfileAsync(Guid id) =>
        await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id)
        ?? throw new DomainException("Profile not found", 404);

    public async Task<Profile> UpdateProfileAsync(Guid id, UpdateProfileRequest req)
    {
        var profile = await GetProfileAsync(id);
        if (req.FullName is not null) profile.FullName = req.FullName.Trim();
        if (req.Phone is not null) profile.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        if (req.PreferredLanguage is "en" or "ml") profile.PreferredLanguage = req.PreferredLanguage;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest req)
    {
        var profile = await GetProfileAsync(id);
        if (!_security.VerifyPassword(profile.PasswordHash, req.CurrentPassword ?? ""))
            throw new DomainException("Current password is incorrect.", 403);
        ValidatePassword(req.NewPassword);
        profile.PasswordHash = _security.HashPassword(req.NewPassword);
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<string> NextMemberCodeAsync()
    {
        var codes = await _db.Profiles
            .Where(p => p.MemberCode.StartsWith("MC"))
            .Select(p => p.MemberCode)
            .ToListAsync();
        var max = 0;
        foreach (var c in codes)
            if (int.TryParse(c[2..], out var n)) max = Math.Max(max, n);
        return $"MC{max + 1:0000}";
    }

    public async Task<string> NextReferenceAsync()
    {
        var year = DateTimeOffset.UtcNow.Year;
        var prefix = $"REG-{year}-";
        var count = await _db.RegistrationRecords.CountAsync(r => r.ReferenceCode.StartsWith(prefix));
        return $"{prefix}{count + 1:0000}";
    }

    public async Task<int> RegistrationFeePaiseAsync()
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == "registration_fee_inr");
        var inr = setting is not null && int.TryParse(setting.Value, out var v) ? v : 50;
        return inr * 100;
    }

    public async Task<DashboardDto> DashboardAsync(Profile me)
    {
        var directSponsored = await _db.Sponsorships.CountAsync(s => s.SponsorId == me.Id);
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var directChildren = placements.Count(p => p.ParentId == me.Id);
        var totalTeam = await CountSubtreeAsync(me.Id, placements);

        var ledger = await _db.RewardLedger.AsNoTracking().Where(l => l.MemberId == me.Id).ToListAsync();
        var total = ledger.Sum(l => l.AmountPaise);
        var pending = ledger.Where(l => l.Status == "pending").Sum(l => l.AmountPaise);
        var settled = ledger.Where(l => l.Status == "settled").Sum(l => l.AmountPaise);

        var reg = await _db.RegistrationRecords.AsNoTracking().FirstOrDefaultAsync(r => r.MemberId == me.Id);
        var sponsor = await _db.Sponsorships.AsNoTracking().Include(s => s.Sponsor).FirstOrDefaultAsync(s => s.SponsoredId == me.Id);
        var placement = placements.FirstOrDefault(p => p.MemberId == me.Id);
        var placementParent = placement?.ParentId is Guid pp
            ? await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pp)
            : null;

        var activity = await RecentActivityAsync(me, placements);

        return new DashboardDto(
            ToProfileDto(me),
            new Dictionary<string, object>
            {
                ["directSponsored"] = directSponsored,
                ["directChildren"] = directChildren,
                ["totalTeam"] = totalTeam,
                ["balancePaise"] = total,
                ["pendingPaise"] = pending,
                ["settledPaise"] = settled
            },
            activity,
            sponsor?.Sponsor?.FullName,
            sponsor?.Sponsor?.MemberCode,
            placementParent?.FullName,
            placementParent?.MemberCode,
            reg?.Status ?? "simulated",
            reg?.ReferenceCode ?? "");
    }

    public async Task<List<ActivityDto>> RecentActivityAsync(Profile me, List<Placement> placements)
    {
        var list = new List<(DateTimeOffset at, string text, string meta, string kind)>();
        var now = DateTimeOffset.UtcNow;

        list.Add((me.CreatedAt, "You joined Money Chain SAS", me.MemberCode, "join"));

        foreach (var p in placements.Where(p => p.ParentId == me.Id))
        {
            var child = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.MemberId);
            if (child is not null)
                list.Add((p.CreatedAt, $"{child.FullName} placed under you", child.MemberCode, "placement"));
        }

        var ledger = await _db.RewardLedger.AsNoTracking().Where(l => l.MemberId == me.Id).ToListAsync();
        foreach (var l in ledger)
            list.Add((l.CreatedAt, $"Simulated reward entry recorded ({l.RewardType})", $"₹{l.AmountPaise / 100m:0.##} · {l.Status}", "reward"));

        return list.OrderByDescending(x => x.at).Take(6)
            .Select(x => new ActivityDto(x.text, x.meta, x.kind))
            .ToList();
    }

    public async Task<int> CountSubtreeAsync(Guid rootId, List<Placement> placements)
    {
        var parentByMember = placements.Where(p => p.ParentId is not null).ToDictionary(p => p.MemberId, p => (Guid?)p.ParentId);
        var all = await _db.Profiles.AsNoTracking().Where(p => p.Role == "member").Select(p => p.Id).ToListAsync();
        return all.Count(id => id != rootId && PlacementService.InSubtree(id, rootId, parentByMember));
    }

    public async Task<List<MemberRowDto>> AuthorizedMembersAsync(Profile me, string? q, string? status)
    {
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var subtree = placements
            .Where(p => p.ParentId is not null)
            .ToDictionary(p => p.MemberId, p => (Guid?)p.ParentId);
        var myId = me.Id;
        var profiles = await _db.Profiles.AsNoTracking().Where(p => p.Role == "member").ToListAsync();
        var allowed = profiles
            .Where(p => p.Id == myId || PlacementService.InSubtree(p.Id, myId, subtree))
            .ToList();

        var sponsorships = await _db.Sponsorships.AsNoTracking().ToListAsync();
        var sponsorById = sponsorships.ToDictionary(s => s.SponsoredId, s => s.SponsorId);
        var profileById = profiles.ToDictionary(p => p.Id, p => p);
        var childCounts = placements.Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return allowed.Select(p => ToMemberRow(p, sponsorById, profileById, placements, childCounts))
            .Where(m => StatusMatch(m.Status, status) && QueryMatch(m, q))
            .OrderBy(m => m.MemberCode)
            .ToList();
    }

    private static bool StatusMatch(string status, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || filter == "all" || status == filter;

    private static bool QueryMatch(MemberRowDto m, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return true;
        var needle = q.Trim().ToLowerInvariant();
        return m.MemberCode.ToLowerInvariant().Contains(needle)
            || m.Name.ToLowerInvariant().Contains(needle)
            || m.Email.ToLowerInvariant().Contains(needle)
            || (m.Phone ?? "").ToLowerInvariant().Contains(needle);
    }

    private MemberRowDto ToMemberRow(
        Profile p,
        Dictionary<Guid, Guid> sponsorById,
        Dictionary<Guid, Profile> profileById,
        List<Placement> placements,
        Dictionary<Guid, int> childCounts)
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
            p.PreferredLanguage,
            p.CreatedAt.ToString("dd MMM yyyy"),
            childCounts.GetValueOrDefault(p.Id));
    }

    public async Task<List<EligibleParentDto>> EligibleParentsAsync() =>
        await _placements.EligibleParentsAsync();

    public async Task<EarningsDto> EarningsAsync(Profile me)
    {
        var ledger = (await _db.RewardLedger.AsNoTracking()
            .Where(l => l.MemberId == me.Id)
            .ToListAsync())
            .OrderByDescending(l => l.CreatedAt)
            .ToList();
        var policy = await _db.RewardPolicies.OrderByDescending(p => p.PolicyVersion).FirstOrDefaultAsync();
        return new EarningsDto(
            ledger.Sum(l => l.AmountPaise),
            ledger.Where(l => l.Status == "pending").Sum(l => l.AmountPaise),
            ledger.Where(l => l.Status == "settled").Sum(l => l.AmountPaise),
            policy?.SimulationEnabled ?? false,
            ledger.Select(ToLedgerRow).ToList());
    }

    public async Task<List<LedgerRowDto>> TransactionsAsync(Profile me, string? type, string? status)
    {
        var regs = await _db.RegistrationRecords.AsNoTracking().Where(r => r.MemberId == me.Id).ToListAsync();
        var ledger = await _db.RewardLedger.AsNoTracking().Where(l => l.MemberId == me.Id).ToListAsync();
        var rows = new List<LedgerRowDto>();
        rows.AddRange(regs.Select(r => new LedgerRowDto(
            r.ReferenceCode, me.MemberCode, me.FullName, "registration", r.AmountPaise,
            r.Status, r.IsSimulated, r.CreatedAt.ToString("dd MMM yyyy"), r.ReferenceCode,
            "Registration entry")));
        rows.AddRange(ledger.Select(l => new LedgerRowDto(
            l.Id.ToString(), me.MemberCode, me.FullName, "reward", l.AmountPaise,
            l.Status, l.IsSimulated, l.CreatedAt.ToString("dd MMM yyyy"), l.TriggerEventId ?? l.Id.ToString(),
            l.Description)));

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
            rows = rows.Where(r => r.Type == type).ToList();
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            rows = rows.Where(r => r.Status == status).ToList();
        return rows.OrderByDescending(r => r.Date).ToList();
    }

    public static LedgerRowDto ToLedgerRow(RewardLedger l) => new(
        l.Id.ToString(), "", "", l.RewardType, l.AmountPaise, l.Status, l.IsSimulated,
        l.CreatedAt.ToString("dd MMM yyyy"), l.TriggerEventId ?? l.Id.ToString(), l.Description);

    public static ProfileDto ToProfileDto(Profile p) => new(
        p.Id.ToString(), p.MemberCode, p.FullName, p.Email, p.Phone,
        p.PreferredLanguage, p.Role, p.AccountStatus, p.CreatedAt.ToString("dd MMM yyyy"));
}
