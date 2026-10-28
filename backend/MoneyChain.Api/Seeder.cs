using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

/// <summary>
/// Idempotent demo-data seeder. Runs on startup; skips when profiles already exist.
/// All data is fictional. Demo credentials are overridable via configuration and are
/// documented in the README (local demo mode only — never for production).
/// </summary>
public class Seeder
{
    private readonly AppDbContext _db;
    private readonly SecurityService _security;
    private readonly IConfiguration _config;

    public Seeder(AppDbContext db, SecurityService security, IConfiguration config)
    {
        _db = db;
        _security = security;
        _config = config;
    }

    public async Task SeedAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        if (await _db.Profiles.AnyAsync()) return;

        var memberPw = _config["Demo:MemberPassword"] ?? "Demo1234";
        var adminPw = _config["Demo:AdminPassword"] ?? "Admin@1234";

        var now = DateTimeOffset.UtcNow;
        var admin = new Profile
        {
            MemberCode = "ADMIN1",
            FullName = "Demo Admin",
            Email = "admin@moneychain.demo",
            PasswordHash = _security.HashPassword(adminPw),
            PreferredLanguage = "en",
            Role = "admin",
            AccountStatus = "active",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Profiles.Add(admin);

        var members = new (string code, string name, string email, string phone, string lang, string status,
            string? sponsor, string? placement, DateTimeOffset joined)[]
        {
            ("MC0001", "Arun Nair",      "arun.nair@example.com",      "+91 98470 12345", "en", "active",    null,      null,      D(2024, 1, 12)),
            ("MC0002", "Deepa Menon",     "deepa.menon@example.com",    "+91 97450 22345", "ml", "active",    "MC0001", "MC0001", D(2024, 2, 3)),
            ("MC0003", "Suresh Kumar",    "suresh.kumar@example.com",   "+91 98950 33456", "ml", "active",    "MC0001", "MC0001", D(2024, 2, 11)),
            ("MC0004", "Aisha Beevi",     "aisha.beevi@example.com",    "+91 96330 44567", "ml", "active",    "MC0001", "MC0001", D(2024, 2, 18)),
            ("MC0005", "Rahul Raj",       "rahul.raj@example.com",      "+91 94470 55678", "en", "active",    "MC0002", "MC0002", D(2024, 3, 5)),
            ("MC0006", "Meera Pillai",    "meera.pillai@example.com",   "+91 98460 66789", "ml", "active",    "MC0002", "MC0002", D(2024, 3, 14)),
            ("MC0007", "Jithin Thomas",   "jithin.thomas@example.com",  "+91 98940 77890", "en", "pending",   "MC0002", "MC0002", D(2024, 3, 22)),
            ("MC0008", "Divya Lakshmi",   "divya.lakshmi@example.com",  "+91 94950 88901", "ml", "active",    "MC0003", "MC0003", D(2024, 3, 30)),
            ("MC0009", "Naveen Varghese", "naveen.varghese@example.com","+91 97440 99012", "en", "active",    "MC0003", "MC0003", D(2024, 4, 6)),
            ("MC0010", "Anoop Krishnan",  "anoop.krishnan@example.com", "+91 98450 00123", "ml", "suspended", "MC0003", "MC0003", D(2024, 4, 15)),
            ("MC0011", "Fathima Nazrin",  "fathima.nazrin@example.com", "+91 96560 11234", "ml", "active",    "MC0004", "MC0004", D(2024, 4, 21)),
            ("MC0012", "Anand Mohan",     "anand.mohan@example.com",    "+91 94960 22345", "en", "active",    "MC0004", "MC0004", D(2024, 5, 2)),
            ("MC0013", "Gouri Devi",      "gouri.devi@example.com",     "+91 98470 33456", "ml", "active",    "MC0003", "MC0005", D(2024, 5, 18)),
        };

        var byCode = new Dictionary<string, Profile> { [admin.MemberCode] = admin };
        foreach (var (code, name, email, phone, lang, status, _, _, joined) in members)
        {
            var p = new Profile
            {
                MemberCode = code,
                FullName = name,
                Email = email,
                Phone = phone,
                PasswordHash = _security.HashPassword(memberPw),
                PreferredLanguage = lang,
                Role = "member",
                AccountStatus = status,
                CreatedAt = joined,
                UpdatedAt = joined
            };
            _db.Profiles.Add(p);
            byCode[code] = p;
        }
        await _db.SaveChangesAsync();

        foreach (var (code, _, _, _, _, _, sponsor, placement, joined) in members)
        {
            if (sponsor is not null && byCode.TryGetValue(sponsor, out var sp))
                _db.Sponsorships.Add(new Sponsorship { SponsorId = sp.Id, SponsoredId = byCode[code].Id, CreatedAt = joined });

            if (placement is not null && byCode.TryGetValue(placement, out var par))
                _db.Placements.Add(new Placement { MemberId = byCode[code].Id, ParentId = par.Id, CreatedAt = joined, UpdatedAt = joined });
        }
        // Root placement record (no parent).
        _db.Placements.Add(new Placement
        {
            MemberId = byCode["MC0001"].Id,
            ParentId = null,
            CreatedAt = D(2024, 1, 12),
            UpdatedAt = D(2024, 1, 12)
        });

        var refNo = 0;
        foreach (var (code, _, _, _, _, status, _, _, joined) in members)
        {
            refNo++;
            _db.RegistrationRecords.Add(new RegistrationRecord
            {
                MemberId = byCode[code].Id,
                AmountPaise = 5000,
                Status = status == "pending" ? "pending" : "approved",
                IsSimulated = true,
                ReferenceCode = $"REG-2024-{refNo:0000}",
                CreatedAt = joined
            });
        }

        _db.RewardPolicies.Add(new RewardPolicy
        {
            PolicyVersion = 1,
            DirectMemberCountThreshold = 3,
            DirectMemberRewardPaise = 10000,
            ChildRewardPaise = 1000,
            ChildRewardTrigger = "child_completes_three_placements",
            SimulationEnabled = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        var arunId = byCode["MC0001"].Id;
        var deepaId = byCode["MC0002"].Id;
        var sureshId = byCode["MC0003"].Id;
        _db.RewardLedger.AddRange(
            new RewardLedger
            {
                MemberId = arunId, RewardType = "direct_member", AmountPaise = 10000, Status = "pending",
                IsSimulated = true, TriggerEventId = "direct:MC0001", PolicyVersion = 1,
                Description = "Third qualifying direct member (threshold 3)", CreatedAt = D(2024, 2, 18)
            },
            new RewardLedger
            {
                MemberId = arunId, RewardType = "child_milestone", AmountPaise = 1000, Status = "settled",
                IsSimulated = true, TriggerEventId = "child:MC0002:milestone", PolicyVersion = 1,
                Description = "Child completed three placements (Deepa Menon)", CreatedAt = D(2024, 3, 22)
            },
            new RewardLedger
            {
                MemberId = arunId, RewardType = "child_milestone", AmountPaise = 1000, Status = "settled",
                IsSimulated = true, TriggerEventId = "child:MC0003:milestone", PolicyVersion = 1,
                Description = "Child completed three placements (Suresh Kumar)", CreatedAt = D(2024, 4, 15)
            });

        _db.AdminAuditLogs.AddRange(
            new AdminAuditLog { AdminId = admin.Id, Action = "placement_corrected", TargetType = "member", TargetId = "MC0013", Reason = "Sponsor requested placement under MC0005", CreatedAt = D(2024, 5, 18, 14, 22) },
            new AdminAuditLog { AdminId = admin.Id, Action = "account_suspended", TargetType = "member", TargetId = "MC0010", Reason = "Duplicate sample account", CreatedAt = D(2024, 4, 15, 10, 5) },
            new AdminAuditLog { AdminId = admin.Id, Action = "placement_created", TargetType = "member", TargetId = "MC0007", Reason = "Placed under Deepa Menon", CreatedAt = D(2024, 3, 22, 11, 30) },
            new AdminAuditLog { AdminId = admin.Id, Action = "registration_approved", TargetType = "member", TargetId = "MC0004", Reason = "Sample record approved", CreatedAt = D(2024, 2, 18, 15, 12) });

        _db.AppSettings.AddRange(
            new AppSetting { Key = "default_language", Value = "en", UpdatedAt = now },
            new AppSetting { Key = "registration_fee_inr", Value = "50", UpdatedAt = now },
            new AppSetting { Key = "maintenance_mode", Value = "false", UpdatedAt = now });

        await _db.SaveChangesAsync();
    }

    private static DateTimeOffset D(int y, int m, int d, int hh = 10, int mm = 0) =>
        new(y, m, d, hh, mm, 0, TimeSpan.Zero);
}
