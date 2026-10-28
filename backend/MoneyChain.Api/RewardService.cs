using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

public class RewardService
{
    private readonly AppDbContext _db;

    public RewardService(AppDbContext db) => _db = db;

    public async Task<RewardPolicy> CurrentPolicyAsync() =>
        await _db.RewardPolicies.OrderByDescending(p => p.PolicyVersion).FirstOrDefaultAsync()
        ?? new RewardPolicy();

    /// <summary>
    /// Deterministically computes simulated rewards for fictional events only.
    /// No-op unless the simulation is explicitly enabled. Duplicate rewards for
    /// the same qualifying event are prevented by the unique TriggerEventId index.
    /// </summary>
    public async Task<(bool ran, int created)> RunSimulationAsync(Guid actorId)
    {
        var policy = await CurrentPolicyAsync();
        if (!policy.SimulationEnabled)
            return (false, 0);

        var created = 0;
        var now = DateTimeOffset.UtcNow;

        // ₹100 one-time per sponsor after the N-th qualifying direct sponsored member.
        var sponsorships = await _db.Sponsorships.AsNoTracking().ToListAsync();
        var profiles = await _db.Profiles.AsNoTracking()
            .Where(p => p.Role == "member")
            .ToDictionaryAsync(p => p.Id, p => p);
        var registrations = await _db.RegistrationRecords.AsNoTracking().ToListAsync();
        var approved = registrations.Where(r => r.Status is "approved" or "simulated").Select(r => r.MemberId).ToHashSet();

        foreach (var group in sponsorships.GroupBy(s => s.SponsorId))
        {
            var qualifying = group.Select(s => s.SponsoredId).Where(approved.Contains).ToList();
            if (qualifying.Count >= policy.DirectMemberCountThreshold)
            {
                var trigger = $"direct:{group.Key}";
                if (!await _db.RewardLedger.AnyAsync(l => l.TriggerEventId == trigger))
                {
                    var name = profiles.TryGetValue(group.Key, out var sp) ? sp.FullName : "";
                    _db.RewardLedger.Add(new RewardLedger
                    {
                        MemberId = group.Key,
                        RewardType = "direct_member",
                        AmountPaise = policy.DirectMemberRewardPaise,
                        Status = "pending",
                        IsSimulated = true,
                        TriggerEventId = trigger,
                        PolicyVersion = policy.PolicyVersion,
                        Description = $"Third qualifying direct member (threshold {policy.DirectMemberCountThreshold})",
                        CreatedAt = now
                    });
                    created++;
                }
            }
        }

        // ₹10 when a direct placement child completes three placements.
        if (policy.ChildRewardTrigger == "child_completes_three_placements")
        {
            var placements = await _db.Placements.AsNoTracking().ToListAsync();
            var childCount = placements.Where(p => p.ParentId is not null)
                .GroupBy(p => p.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var placement in placements.Where(p => p.ParentId is not null))
            {
                var sponsorId = placement.ParentId!.Value;
                var childId = placement.MemberId;
                if (childCount.GetValueOrDefault(childId) >= 3)
                {
                    var trigger = $"child:{childId}:milestone";
                    if (!await _db.RewardLedger.AnyAsync(l => l.TriggerEventId == trigger))
                    {
                        var childName = profiles.TryGetValue(childId, out var c) ? c.FullName : "";
                        _db.RewardLedger.Add(new RewardLedger
                        {
                            MemberId = sponsorId,
                            RewardType = "child_milestone",
                            AmountPaise = policy.ChildRewardPaise,
                            Status = "settled",
                            IsSimulated = true,
                            TriggerEventId = trigger,
                            PolicyVersion = policy.PolicyVersion,
                            Description = $"Child completed three placements ({childName})",
                            CreatedAt = now
                        });
                        created++;
                    }
                }
            }
        }

        if (created > 0)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminId = actorId,
                Action = "simulation_run",
                TargetType = "reward_ledger",
                TargetId = created.ToString(),
                Reason = "Deterministic simulated reward calculation",
                CreatedAt = now
            });
            await _db.SaveChangesAsync();
        }

        return (true, created);
    }

    /// <summary>Records a reversal as a new immutable negative entry (never edits history).</summary>
    public async Task<RewardLedger> ReverseAsync(Guid ledgerId, string reason, Guid actorId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required for a reversal.");
        var original = await _db.RewardLedger.FirstOrDefaultAsync(l => l.Id == ledgerId)
            ?? throw new DomainException("Ledger entry not found", 404);
        if (original.AmountPaise <= 0)
            throw new DomainException("Only positive entries can be reversed.");

        var reversal = new RewardLedger
        {
            MemberId = original.MemberId,
            RewardType = "reversal",
            AmountPaise = -original.AmountPaise,
            Status = "settled",
            IsSimulated = true,
            TriggerEventId = $"reversal:{original.Id}",
            PolicyVersion = original.PolicyVersion,
            Description = $"Reversal of {original.Id} — {reason}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.RewardLedger.Add(reversal);
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminId = actorId,
            Action = "reward_reversal",
            TargetType = "reward_ledger",
            TargetId = original.Id.ToString(),
            Reason = reason,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { reversalId = reversal.Id }),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
        return reversal;
    }
}
