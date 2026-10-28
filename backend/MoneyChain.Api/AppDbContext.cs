using Microsoft.EntityFrameworkCore;

namespace MoneyChain.Api;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Sponsorship> Sponsorships => Set<Sponsorship>();
    public DbSet<Placement> Placements => Set<Placement>();
    public DbSet<RegistrationRecord> RegistrationRecords => Set<RegistrationRecord>();
    public DbSet<RewardLedger> RewardLedger => Set<RewardLedger>();
    public DbSet<RewardPolicy> RewardPolicies => Set<RewardPolicy>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Profile>(e =>
        {
            e.HasIndex(p => p.Email).IsUnique();
            e.HasIndex(p => p.MemberCode).IsUnique();
        });

        b.Entity<Sponsorship>(e =>
        {
            e.HasIndex(s => s.SponsoredId).IsUnique();
            e.HasOne(s => s.Sponsor).WithMany().HasForeignKey(s => s.SponsorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Sponsored).WithMany().HasForeignKey(s => s.SponsoredId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Placement>(e =>
        {
            e.HasIndex(p => p.MemberId).IsUnique();
            e.HasOne(p => p.Member).WithMany().HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Parent).WithMany().HasForeignKey(p => p.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RegistrationRecord>(e =>
        {
            e.HasIndex(r => r.ReferenceCode).IsUnique();
            e.HasOne(r => r.Member).WithMany().HasForeignKey(r => r.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RewardLedger>(e =>
        {
            e.HasIndex(r => r.TriggerEventId).IsUnique();
            e.HasOne(r => r.Member).WithMany().HasForeignKey(r => r.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RewardPolicy>(e => e.HasIndex(p => p.PolicyVersion).IsUnique());

        b.Entity<WithdrawalRequest>(e =>
            e.HasOne<Profile>().WithMany().HasForeignKey(w => w.MemberId).OnDelete(DeleteBehavior.Restrict));

        b.Entity<AdminAuditLog>(e =>
            e.HasOne(a => a.Admin).WithMany().HasForeignKey(a => a.AdminId).OnDelete(DeleteBehavior.Restrict));

        b.Entity<AppSetting>(e => e.HasKey(s => s.Key));
    }
}
