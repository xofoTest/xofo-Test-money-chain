using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MoneyChain.Api;
using Xunit;

namespace MoneyChain.Tests;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
public static class Db
{
    public static (AppDbContext db, string path) New()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mc-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (db, path);
    }

    public static Profile AddProfile(AppDbContext db, string code, string status = "active", string role = "member")
    {
        var p = new Profile
        {
            MemberCode = code,
            FullName = $"Member {code}",
            Email = $"{code.ToLowerInvariant()}@test.local",
            PasswordHash = "x",
            Role = role,
            AccountStatus = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Profiles.Add(p);
        db.SaveChanges();
        return p;
    }

    public static SecurityService Security() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
}

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mc-int-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", _dbPath);
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}

// ---------------------------------------------------------------------------
// Placement rules
// ---------------------------------------------------------------------------
public class PlacementTests
{
    [Fact]
    public async Task Three_child_limit_is_enforced()
    {
        var (db, path) = Db.New();
        try
        {
            var svc = new PlacementService(db);
            var root = Db.AddProfile(db, "MC0001");

            for (var i = 0; i < 3; i++)
            {
                var child = Db.AddProfile(db, $"MC{i + 2:0000}");
                await svc.PlaceMemberAsync(child.Id, root.Id, "test", root.Id, isCorrection: false);
            }

            var fourth = Db.AddProfile(db, "MC0009");
            var ex = await Assert.ThrowsAsync<DomainException>(
                () => svc.PlaceMemberAsync(fourth.Id, root.Id, "test", root.Id, isCorrection: false));
            Assert.Contains("already has 3 children", ex.Message);
            Assert.Equal(3, await svc.ChildCountAsync(root.Id));
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Duplicate_placement_is_rejected()
    {
        var (db, path) = Db.New();
        try
        {
            var svc = new PlacementService(db);
            var root = Db.AddProfile(db, "MC0001");
            var child = Db.AddProfile(db, "MC0002");
            await svc.PlaceMemberAsync(child.Id, root.Id, "test", root.Id, isCorrection: false);

            var ex = await Assert.ThrowsAsync<DomainException>(
                () => svc.PlaceMemberAsync(child.Id, root.Id, "test", root.Id, isCorrection: false));
            Assert.Contains("duplicate placement", ex.Message);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Self_parent_is_rejected()
    {
        var (db, path) = Db.New();
        try
        {
            var svc = new PlacementService(db);
            var root = Db.AddProfile(db, "MC0001");
            var ex = await Assert.ThrowsAsync<DomainException>(
                () => svc.PlaceMemberAsync(root.Id, root.Id, "test", root.Id, isCorrection: false));
            Assert.Contains("own parent", ex.Message);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Cycles_are_prevented()
    {
        var (db, path) = Db.New();
        try
        {
            var svc = new PlacementService(db);
            var a = Db.AddProfile(db, "MC0001");
            var b = Db.AddProfile(db, "MC0002");
            var c = Db.AddProfile(db, "MC0003");
            await svc.PlaceMemberAsync(b.Id, a.Id, "t", a.Id, isCorrection: false);
            await svc.PlaceMemberAsync(c.Id, b.Id, "t", a.Id, isCorrection: false);

            // Re-parent B under its own descendant C must fail.
            var ex = await Assert.ThrowsAsync<DomainException>(
                () => svc.PlaceMemberAsync(b.Id, c.Id, "t", a.Id, isCorrection: true));
            Assert.Contains("descendant", ex.Message);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Concurrent_placements_never_exceed_three_children()
    {
        var (db, path) = Db.New();
        try
        {
            var rootId = Db.AddProfile(db, "MC0001").Id;
            var children = new List<Guid>();
            for (var i = 0; i < 8; i++)
                children.Add(Db.AddProfile(db, $"MC{i + 2:0000}").Id);

            var tasks = children.Select(async id =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
                await using var c = new AppDbContext(options);
                var svc = new PlacementService(c);
                try { await svc.PlaceMemberAsync(id, rootId, "t", rootId, isCorrection: false); return true; }
                catch (DomainException) { return false; }
            });

            var results = await Task.WhenAll(tasks);
            Assert.Equal(3, results.Count(r => r));

            await using var check = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
            Assert.Equal(3, await check.Placements.CountAsync(p => p.ParentId == rootId));
        }
        finally { db.Dispose(); File.Delete(path); }
    }
}

// ---------------------------------------------------------------------------
// Reward simulation & currency
// ---------------------------------------------------------------------------
public class RewardTests
{
    [Fact]
    public async Task Simulation_is_disabled_by_default_and_creates_nothing()
    {
        var (db, path) = Db.New();
        try
        {
            var svc = new RewardService(db);
            var actor = Db.AddProfile(db, "ADMIN", role: "admin");
            var (ran, created) = await svc.RunSimulationAsync(actor.Id);
            Assert.False(ran);
            Assert.Equal(0, created);
            Assert.Equal(0, await db.RewardLedger.CountAsync());
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Direct_member_reward_is_one_time_and_uses_paise()
    {
        var (db, path) = Db.New();
        try
        {
            db.RewardPolicies.Add(new RewardPolicy
            {
                PolicyVersion = 1, DirectMemberCountThreshold = 3,
                DirectMemberRewardPaise = 10000, ChildRewardPaise = 1000,
                ChildRewardTrigger = "child_completes_three_placements", SimulationEnabled = true
            });
            await db.SaveChangesAsync();

            var sponsor = Db.AddProfile(db, "MC0001");
            for (var i = 0; i < 3; i++)
            {
                var m = Db.AddProfile(db, $"MC{i + 2:0000}");
                db.Sponsorships.Add(new Sponsorship { SponsorId = sponsor.Id, SponsoredId = m.Id, CreatedAt = DateTimeOffset.UtcNow });
                db.RegistrationRecords.Add(new RegistrationRecord { MemberId = m.Id, AmountPaise = 5000, Status = "approved", IsSimulated = true, ReferenceCode = $"R{i}", CreatedAt = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync();

            var svc = new RewardService(db);
            var actor = Db.AddProfile(db, "ADMIN", role: "admin");
            var (_, created) = await svc.RunSimulationAsync(actor.Id);
            Assert.Equal(1, created);

            var entry = await db.RewardLedger.SingleAsync(l => l.RewardType == "direct_member");
            Assert.Equal(10000, entry.AmountPaise); // ₹100 in paise

            // Second run must not duplicate.
            var (_, again) = await svc.RunSimulationAsync(actor.Id);
            Assert.Equal(0, again);
            Assert.Equal(1, await db.RewardLedger.CountAsync(l => l.RewardType == "direct_member"));
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Child_milestone_reward_triggers_when_a_child_completes_three_placements()
    {
        var (db, path) = Db.New();
        try
        {
            db.RewardPolicies.Add(new RewardPolicy
            {
                PolicyVersion = 1, DirectMemberCountThreshold = 3,
                DirectMemberRewardPaise = 10000, ChildRewardPaise = 1000,
                ChildRewardTrigger = "child_completes_three_placements", SimulationEnabled = true
            });
            await db.SaveChangesAsync();

            var sponsor = Db.AddProfile(db, "MC0001");
            var child = Db.AddProfile(db, "MC0002");
            db.Placements.Add(new Placement { MemberId = child.Id, ParentId = sponsor.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            for (var i = 0; i < 3; i++)
            {
                var grand = Db.AddProfile(db, $"MC{i + 3:0000}");
                db.Placements.Add(new Placement { MemberId = grand.Id, ParentId = child.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync();

            var svc = new RewardService(db);
            var actor = Db.AddProfile(db, "ADMIN", role: "admin");
            var (_, created) = await svc.RunSimulationAsync(actor.Id);
            Assert.Equal(1, created);
            Assert.Equal(1000, (await db.RewardLedger.SingleAsync(l => l.RewardType == "child_milestone")).AmountPaise);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task Reversal_records_a_negative_entry_instead_of_editing()
    {
        var (db, path) = Db.New();
        try
        {
            var member = Db.AddProfile(db, "MC0001");
            var entry = new RewardLedger
            {
                MemberId = member.Id, RewardType = "direct_member", AmountPaise = 10000,
                Status = "pending", IsSimulated = true, TriggerEventId = "direct:MC0001",
                PolicyVersion = 1, Description = "test", CreatedAt = DateTimeOffset.UtcNow
            };
            db.RewardLedger.Add(entry);
            await db.SaveChangesAsync();

            var svc = new RewardService(db);
            var actor = Db.AddProfile(db, "ADMIN", role: "admin");
            await svc.ReverseAsync(entry.Id, "Duplicate reward", actor.Id);

            var original = await db.RewardLedger.FindAsync(entry.Id);
            Assert.Equal(10000, original!.AmountPaise); // untouched
            var reversal = await db.RewardLedger.SingleAsync(l => l.RewardType == "reversal");
            Assert.Equal(-10000, reversal.AmountPaise);
            Assert.Contains("Duplicate reward", reversal.Description);
        }
        finally { db.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Passwords_are_hashed_and_verify_correctly()
    {
        var security = Db.Security();
        var hash = security.HashPassword("Demo1234");
        Assert.NotEqual("Demo1234", hash);
        Assert.True(security.VerifyPassword(hash, "Demo1234"));
        Assert.False(security.VerifyPassword(hash, "wrong"));
    }

    [Fact]
    public async Task Placement_changes_are_audited()
    {
        var (db, path) = Db.New();
        try
        {
            var svc = new PlacementService(db);
            var root = Db.AddProfile(db, "MC0001");
            var child = Db.AddProfile(db, "MC0002");
            await svc.PlaceMemberAsync(child.Id, root.Id, "reason here", root.Id, isCorrection: false);

            var audit = await db.AdminAuditLogs.SingleAsync();
            Assert.Equal("placement_created", audit.Action);
            Assert.Equal("MC0002", audit.TargetId);
            Assert.Equal("reason here", audit.Reason);
        }
        finally { db.Dispose(); File.Delete(path); }
    }
}

// ---------------------------------------------------------------------------
// Integration: auth, authorization, disabled payments
// ---------------------------------------------------------------------------
public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ApiIntegrationTests(ApiFactory factory) => _factory = factory;

    private static async Task<string> Login(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Admin_endpoints_are_role_gated()
    {
        var client = _factory.CreateClient();

        // No token -> 401
        var anon = await client.GetAsync("/api/admin/overview");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        // Member token -> 403
        var memberToken = await Login(client, "arun.nair@example.com", "Demo1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var member = await client.GetAsync("/api/admin/overview");
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);

        // Admin token -> 200
        var adminToken = await Login(client, "admin@moneychain.demo", "Admin@1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var admin = await client.GetAsync("/api/admin/overview");
        var adminBody = await admin.Content.ReadAsStringAsync();
        Assert.True(admin.StatusCode == HttpStatusCode.OK, adminBody);
    }

    [Fact]
    public async Task Member_registration_and_login_work()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test User",
            email = "new.member@example.com",
            phone = "+91 90000 00000",
            password = "Password123",
            sponsorCode = "MC0004",
            preferredLanguage = "ml",
            termsAccepted = true
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var token = await Login(client, "new.member@example.com", "Password123");
        Assert.False(string.IsNullOrEmpty(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Live_payments_and_payouts_remain_disabled()
    {
        var client = _factory.CreateClient();
        var token = await Login(client, "admin@moneychain.demo", "Admin@1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/admin/settings");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("livePaymentsEnabled").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("livePayoutsEnabled").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("rewardSimulationEnabled").GetBoolean());
    }

    [Fact]
    public async Task Three_child_limit_is_enforced_through_the_api()
    {
        var client = _factory.CreateClient();
        // Seed already has MC0001 at 3 direct placement children.
        var token = await Login(client, "admin@moneychain.demo", "Admin@1234");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.PostAsJsonAsync("/api/admin/placements/correct", new
        {
            memberCode = "MC0004",
            newParentCode = "MC0003", // MC0003 already has three children
            reason = "test invalid move"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
