using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

public class PlacementService
{
    // Serializes in-process placement writes so the three-child limit cannot be
    // raced past even before the database constraint/transaction is committed.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly AppDbContext _db;

    public PlacementService(AppDbContext db) => _db = db;

    public const int MaxChildren = 3;

    /// <summary>Validate and write a placement inside a transaction. Returns the placement.</summary>
    public async Task<Placement> PlaceMemberAsync(
        Guid memberId, Guid? parentId, string reason, Guid actorId, bool isCorrection)
    {
        await Gate.WaitAsync();
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var member = await _db.Profiles.FindAsync(memberId)
                ?? throw new DomainException("Member not found", 404);

            if (parentId == memberId)
                throw new DomainException("A member cannot be their own parent.");

            var existing = await _db.Placements.FirstOrDefaultAsync(p => p.MemberId == memberId);
            if (existing is not null && !isCorrection)
                throw new DomainException("This member already has a placement record (duplicate placement).");

            if (parentId is Guid pid)
            {
                var parent = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == pid)
                    ?? throw new DomainException("Placement parent not found", 404);

                if (await IsDescendantAsync(parentId.Value, memberId))
                    throw new DomainException("Cannot place a member beneath one of their own descendants.");

                if (await IsDescendantAsync(memberId, parentId.Value))
                    throw new DomainException("Placement would create a cycle in the tree.");

                var childCount = await _db.Placements.CountAsync(p => p.ParentId == pid);
                if (childCount >= MaxChildren)
                    throw new DomainException(
                        $"That placement parent already has {MaxChildren} children. Choose another eligible node.");
            }

            if (existing is not null)
            {
                existing.ParentId = parentId;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var placement = new Placement
                {
                    MemberId = memberId,
                    ParentId = parentId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.Placements.Add(placement);
            }

            _db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminId = actorId,
                Action = isCorrection ? "placement_corrected" : "placement_created",
                TargetType = "member",
                TargetId = member.MemberCode,
                Reason = reason,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new { parentId, memberCode = member.MemberCode }),
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return existing ?? new Placement { MemberId = memberId, ParentId = parentId };
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>True if <paramref name="ancestor"/> is reachable by walking up from <paramref name="node"/>.</summary>
    public async Task<bool> IsDescendantAsync(Guid node, Guid ancestor)
    {
        var placements = await _db.Placements.AsNoTracking().ToDictionaryAsync(p => p.MemberId, p => p.ParentId);
        var current = node;
        var seen = new HashSet<Guid>();
        while (current != Guid.Empty && seen.Add(current))
        {
            if (!placements.TryGetValue(current, out var parent) || parent is null)
                return false;
            if (parent == ancestor)
                return true;
            current = parent.Value;
        }
        return false;
    }

    public async Task<int> ChildCountAsync(Guid memberId) =>
        await _db.Placements.CountAsync(p => p.ParentId == memberId);

    public async Task<List<Placement>> LoadTreeAsync()
    {
        return (await _db.Placements.AsNoTracking().ToListAsync()).OrderBy(p => p.CreatedAt).ToList();
    }

    /// <summary>Returns placement rows for the subtree rooted at <paramref name="rootCode"/> (or the whole tree).</summary>
    public async Task<List<TreeDto>> BuildTreeAsync(string? rootCode)
    {
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var profiles = await _db.Profiles.AsNoTracking()
            .Where(p => p.Role == "member")
            .ToDictionaryAsync(p => p.Id, p => p);
        var sponsors = await _db.Sponsorships.AsNoTracking()
            .ToDictionaryAsync(s => s.SponsoredId, s => s.SponsorId);

        var childCount = placements.Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var parentByMember = placements.ToDictionary(p => p.MemberId, p => p.ParentId);

        Guid? rootId = null;
        if (!string.IsNullOrWhiteSpace(rootCode))
        {
            var root = profiles.Values.FirstOrDefault(p => p.MemberCode == rootCode);
            rootId = root?.Id;
        }

        var rows = new List<TreeDto>();
        foreach (var p in profiles.Values)
        {
            if (rootId is Guid rid && !InSubtree(p.Id, rid, parentByMember))
                continue;
            var parentProfile = parentIdLookup(p, parentByMember, profiles);
            string? sponsorCode = null, sponsorName = null;
            if (sponsors.TryGetValue(p.Id, out var spId) && profiles.TryGetValue(spId, out var sp))
            {
                sponsorCode = sp.MemberCode;
                sponsorName = sp.FullName;
            }
            rows.Add(new TreeDto(
                p.MemberCode,
                p.FullName,
                p.AccountStatus,
                parentProfile?.MemberCode,
                childCount.GetValueOrDefault(p.Id),
                childCount.GetValueOrDefault(p.Id) > 0,
                sponsorCode,
                sponsorName));
        }
        return rows.OrderBy(r => rootId is null ? 0 : r.MemberCode == rootCode ? 0 : 1).ToList();

        static Profile? parentIdLookup(Profile p, Dictionary<Guid, Guid?> map, Dictionary<Guid, Profile> profiles)
        {
            if (map.TryGetValue(p.Id, out var parentId) && parentId is Guid gid && profiles.TryGetValue(gid, out var parent))
                return parent;
            return null;
        }
    }

    public static bool InSubtree(Guid memberId, Guid rootId, Dictionary<Guid, Guid?> parentByMember)
    {
        if (memberId == rootId) return true;
        var current = memberId;
        var seen = new HashSet<Guid>();
        while (seen.Add(current))
        {
            if (!parentByMember.TryGetValue(current, out var parent) || parent is null)
                return false;
            if (parent == rootId) return true;
            current = parent.Value;
        }
        return false;
    }

    public async Task<List<EligibleParentDto>> EligibleParentsAsync()
    {
        var profiles = await _db.Profiles.AsNoTracking()
            .Where(p => p.Role == "member" && p.AccountStatus != "suspended")
            .ToListAsync();
        var counts = await _db.Placements.AsNoTracking()
            .Where(p => p.ParentId != null)
            .GroupBy(p => p.ParentId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId, x => x.Count);

        return profiles
            .Where(p => counts.GetValueOrDefault(p.Id) < MaxChildren)
            .Select(p => new EligibleParentDto(
                p.MemberCode, p.FullName,
                counts.GetValueOrDefault(p.Id),
                MaxChildren - counts.GetValueOrDefault(p.Id)))
            .OrderBy(p => p.MemberCode)
            .ToList();
    }

    /// <summary>Detect invalid relationships: self-parents, cycles, and over-full nodes.</summary>
    public async Task<List<string>> ValidateTreeAsync()
    {
        var placements = await _db.Placements.AsNoTracking().ToListAsync();
        var issues = new List<string>();
        var parentByMember = placements.ToDictionary(p => p.MemberId, p => p.ParentId);

        foreach (var p in placements)
        {
            if (p.ParentId == p.MemberId)
                issues.Add($"Self-parent: {p.MemberId}");
            if (p.ParentId is Guid pid)
            {
                var cur = pid;
                var seen = new HashSet<Guid>();
                while (seen.Add(cur))
                {
                    if (cur == p.MemberId) { issues.Add($"Cycle involving: {p.MemberId}"); break; }
                    if (!parentByMember.TryGetValue(cur, out var up) || up is null) break;
                    cur = up.Value;
                }
            }
        }
        var overFull = placements.Where(p => p.ParentId is not null)
            .GroupBy(p => p.ParentId!.Value)
            .Where(g => g.Count() > MaxChildren)
            .Select(g => $"Over-full node: {g.Key} has {g.Count()} children");
        issues.AddRange(overFull);
        return issues.Distinct().ToList();
    }
}
