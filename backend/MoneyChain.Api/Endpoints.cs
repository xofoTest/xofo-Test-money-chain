using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

public static class Endpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ---------- Auth & profile ----------
        api.MapPost("/auth/register", async (RegisterRequest req, MemberService members) =>
        {
            var (profile, token) = await members.RegisterAsync(req);
            return Results.Ok(new AuthResponse(token, MemberService.ToProfileDto(profile)));
        });

        api.MapPost("/auth/login", async (LoginRequest req, MemberService members) =>
        {
            var (profile, token) = await members.LoginAsync(req);
            return Results.Ok(new AuthResponse(token, MemberService.ToProfileDto(profile)));
        });

        api.MapGet("/auth/me", async (ClaimsPrincipal user, MemberService members) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            return Results.Ok(MemberService.ToProfileDto(me));
        }).RequireAuthorization();

        api.MapPost("/auth/change-password", async (ChangePasswordRequest req, ClaimsPrincipal user, MemberService members) =>
        {
            await members.ChangePasswordAsync(SecurityService.UserId(user), req);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization();

        api.MapPut("/profile", async (UpdateProfileRequest req, ClaimsPrincipal user, MemberService members) =>
        {
            var p = await members.UpdateProfileAsync(SecurityService.UserId(user), req);
            return Results.Ok(MemberService.ToProfileDto(p));
        }).RequireAuthorization();

        // ---------- Member ----------
        api.MapGet("/dashboard", async (ClaimsPrincipal user, MemberService members) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            return Results.Ok(await members.DashboardAsync(me));
        }).RequireAuthorization();

        api.MapGet("/members", async (ClaimsPrincipal user, MemberService members, string? q, string? status) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            return Results.Ok(await members.AuthorizedMembersAsync(me, q, status));
        }).RequireAuthorization();

        api.MapGet("/tree", async (ClaimsPrincipal user, MemberService members, PlacementService placements, AppDbContext db, string? root) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            if (me.Role == "admin")
                return Results.Ok(await placements.BuildTreeAsync(root));

            if (!string.IsNullOrWhiteSpace(root))
            {
                var all = await placements.LoadTreeAsync();
                var map = all.Where(p => p.ParentId is not null).ToDictionary(p => p.MemberId, p => (Guid?)p.ParentId);
                var rootProfile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.MemberCode == root && p.Role == "member");
                if (rootProfile is null || (rootProfile.Id != me.Id && !PlacementService.InSubtree(rootProfile.Id, me.Id, map)))
                    return Results.Json(new { error = "You are not authorized to view this subtree." }, statusCode: 403);
            }
            return Results.Ok(await placements.BuildTreeAsync(me.MemberCode));
        }).RequireAuthorization();

        api.MapGet("/eligible-parents", async (ClaimsPrincipal user, MemberService members) =>
        {
            _ = await members.GetProfileAsync(SecurityService.UserId(user));
            return Results.Ok(await members.EligibleParentsAsync());
        }).RequireAuthorization();

        api.MapPost("/members", async (AddMemberRequest req, ClaimsPrincipal user, MemberService members) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            if (me.AccountStatus != "active")
                throw new DomainException("Only active members can add new members.", 403);
            var (profile, _) = await members.AddMemberAsync(req, me);
            return Results.Ok(MemberService.ToProfileDto(profile));
        }).RequireAuthorization();

        api.MapGet("/earnings", async (ClaimsPrincipal user, MemberService members) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            return Results.Ok(await members.EarningsAsync(me));
        }).RequireAuthorization();

        api.MapGet("/transactions", async (ClaimsPrincipal user, MemberService members, string? type, string? status) =>
        {
            var me = await members.GetProfileAsync(SecurityService.UserId(user));
            return Results.Ok(await members.TransactionsAsync(me, type, status));
        }).RequireAuthorization();

        // ---------- Admin ----------
        var admin = api.MapGroup("/admin").RequireAuthorization("AdminOnly");

        admin.MapGet("/overview", async (AdminService adm) => Results.Ok(await adm.OverviewAsync()));

        admin.MapGet("/members", async (AdminService adm, string? q, string? status) =>
            Results.Ok(await adm.MembersAsync(q, status)));

        admin.MapGet("/members/export", async (AdminService adm, string? q, string? status) =>
        {
            var rows = await adm.MembersAsync(q, status);
            var csv = new StringBuilder("MemberID,Name,Email,Phone,Status,Sponsor,PlacementParent,Joined\n");
            foreach (var m in rows)
            {
                csv.Append($"{m.MemberCode},\"{m.Name}\",{m.Email},{m.Phone ?? ""},{m.Status},{m.SponsorCode},{m.PlacementParentCode ?? ""},{m.Joined}\n");
            }
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "moneychain-members.csv");
        });

        admin.MapPost("/members/{code}/status", async (string code, StatusChangeRequest req, ClaimsPrincipal user, AdminService adm) =>
        {
            var p = await adm.ChangeStatusAsync(SecurityService.UserId(user), code, req);
            return Results.Ok(MemberService.ToProfileDto(p));
        });

        admin.MapGet("/tree", async (PlacementService placements, string? root) =>
            Results.Ok(await placements.BuildTreeAsync(root)));

        admin.MapGet("/placements/slots", async (AdminService adm) => Results.Ok(await adm.SlotsAsync()));

        admin.MapGet("/placements/history", async (AdminService adm) => Results.Ok(await adm.PlacementHistoryAsync()));

        admin.MapPost("/placements/validate", async (AdminService adm) =>
            Results.Ok(new { issues = await adm.ValidateTreeAsync() }));

        admin.MapPost("/placements/correct", async (PlacementCorrectionRequest req, ClaimsPrincipal user, AdminService adm) =>
        {
            await adm.CorrectPlacementAsync(SecurityService.UserId(user), req);
            return Results.Ok(new { ok = true });
        });

        admin.MapGet("/transactions", async (AdminService adm, string? type, string? status, string? q) =>
            Results.Ok(await adm.TransactionsAsync(type, status, q)));

        admin.MapGet("/settings", async (AdminService adm) => Results.Ok(await adm.GetSettingsAsync()));

        admin.MapPut("/settings", async (SettingsUpdateRequest req, ClaimsPrincipal user, AdminService adm) =>
            Results.Ok(await adm.UpdateSettingsAsync(SecurityService.UserId(user), req)));

        admin.MapPost("/simulation/run", async (ClaimsPrincipal user, RewardService rewards) =>
        {
            var (ran, created) = await rewards.RunSimulationAsync(SecurityService.UserId(user));
            return Results.Ok(new { ran, created, message = ran ? $"Created {created} simulated entries." : "Simulation is disabled." });
        });

        admin.MapPost("/simulation/reverse", async (ReversalRequest req, ClaimsPrincipal user, RewardService rewards) =>
        {
            await rewards.ReverseAsync(req.LedgerId, req.Reason, SecurityService.UserId(user));
            return Results.Ok(new { ok = true });
        });

        admin.MapGet("/audit", async (AdminService adm) => Results.Ok(await adm.RecentAuditAsync(50)));
    }
}
