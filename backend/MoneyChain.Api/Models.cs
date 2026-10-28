using System.Text.Json.Serialization;

namespace MoneyChain.Api;

// ---------------------------------------------------------------------------
// Domain entities (EF Core). Currency is stored as integer paise everywhere.
// ---------------------------------------------------------------------------
public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MemberCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    /// <summary>"member" | "admin"</summary>
    public string Role { get; set; } = "member";
    /// <summary>"pending" | "active" | "suspended"</summary>
    public string AccountStatus { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Sponsorship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SponsorId { get; set; }
    public Profile Sponsor { get; set; } = null!;
    public Guid SponsoredId { get; set; }
    public Profile Sponsored { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class Placement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Profile Member { get; set; } = null!;
    /// <summary>Null only for the root member.</summary>
    public Guid? ParentId { get; set; }
    public Profile? Parent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class RegistrationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Profile Member { get; set; } = null!;
    public int AmountPaise { get; set; }
    /// <summary>"simulated" | "pending" | "approved"</summary>
    public string Status { get; set; } = "simulated";
    public bool IsSimulated { get; set; } = true;
    public string ReferenceCode { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class RewardLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Profile Member { get; set; } = null!;
    /// <summary>"direct_member" | "child_milestone" | "reversal"</summary>
    public string RewardType { get; set; } = "";
    public int AmountPaise { get; set; }
    /// <summary>"pending" | "settled"</summary>
    public string Status { get; set; } = "pending";
    public bool IsSimulated { get; set; } = true;
    public string? TriggerEventId { get; set; }
    public int PolicyVersion { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class RewardPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PolicyVersion { get; set; } = 1;
    public int DirectMemberCountThreshold { get; set; } = 3;
    public int DirectMemberRewardPaise { get; set; } = 10000;
    public int ChildRewardPaise { get; set; } = 1000;
    /// <summary>Explicit trigger: a child completes three placements.</summary>
    public string ChildRewardTrigger { get; set; } = "child_completes_three_placements";
    public bool SimulationEnabled { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Schema-only. Creation and processing stay disabled in the prototype.</summary>
public class WithdrawalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public int AmountPaise { get; set; }
    public string Status { get; set; } = "disabled";
    public DateTimeOffset CreatedAt { get; set; }
}

public class AdminAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminId { get; set; }
    public Profile Admin { get; set; } = null!;
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string? Reason { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

// ---------------------------------------------------------------------------
// Domain error
// ---------------------------------------------------------------------------
public class DomainException : Exception
{
    public int StatusCode { get; }
    public DomainException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

// ---------------------------------------------------------------------------
// Request / response contracts (camelCase JSON by default)
// ---------------------------------------------------------------------------
public record RegisterRequest(
    string FullName,
    string Email,
    string? Phone,
    string Password,
    string SponsorCode,
    string PreferredLanguage,
    bool TermsAccepted);

public record AddMemberRequest(
    string FullName,
    string Email,
    string? Phone,
    string PreferredLanguage,
    string PlacementParentCode,
    bool TermsAccepted);

public record LoginRequest(string Email, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UpdateProfileRequest(string? FullName, string? Phone, string? PreferredLanguage);
public record StatusChangeRequest(string Status, string Reason);
public record PlacementCorrectionRequest(string MemberCode, string? NewParentCode, string Reason);
public record SettingsUpdateRequest(
    string? DefaultLanguage,
    int? RegistrationFeeInr,
    int? DirectMemberCountThreshold,
    int? DirectMemberRewardInr,
    int? ChildRewardInr,
    string? ChildRewardTrigger,
    bool? RewardSimulationEnabled,
    bool? MaintenanceMode);
public record ReversalRequest(Guid LedgerId, string Reason);

public record AuthResponse(string Token, ProfileDto Profile);
public record ProfileDto(
    string Id,
    string MemberCode,
    string FullName,
    string Email,
    string? Phone,
    string PreferredLanguage,
    string Role,
    string AccountStatus,
    string CreatedAt);
public record TreeDto(
    string MemberCode,
    string Name,
    string Status,
    string? ParentCode,
    int ChildCount,
    bool HasChildren,
    string? SponsorCode,
    string? SponsorName);
public record MemberRowDto(
    string MemberCode,
    string Name,
    string Email,
    string? Phone,
    string Status,
    string SponsorCode,
    string SponsorName,
    string? PlacementParentCode,
    string? PlacementParentName,
    string PreferredLanguage,
    string Joined,
    int ChildCount);
public record LedgerRowDto(
    string Id,
    string MemberCode,
    string MemberName,
    string Type,
    int AmountPaise,
    string Status,
    bool IsSimulated,
    string Date,
    string Reference,
    string Reason);
public record AuditRowDto(
    string Date,
    string Admin,
    string Action,
    string Target,
    string? Reason);
public record ActivityDto(string Text, string Meta, string Kind);
public record DashboardDto(
    ProfileDto Member,
    Dictionary<string, object> Stats,
    List<ActivityDto> Activity,
    string? SponsorName,
    string? SponsorCode,
    string? PlacementParentName,
    string? PlacementParentCode,
    string RegistrationStatus,
    string RegistrationReference);
public record EarningsDto(
    int TotalPaise,
    int PendingPaise,
    int SettledPaise,
    bool SimulationEnabled,
    List<LedgerRowDto> Entries);
public record AdminOverviewDto(
    int TotalMembers,
    int ActiveMembers,
    int SuspendedMembers,
    int PendingMembers,
    int RegistrationRecords,
    int TreeNodes,
    int FullNodes,
    int OpenSlots,
    int RewardsTotalPaise,
    int RewardsPendingPaise,
    int RewardsSettledPaise,
    bool SimulationEnabled,
    List<AuditRowDto> RecentActivity);
public record SettingsDto(
    string DefaultLanguage,
    int RegistrationFeeInr,
    int DirectMemberCountThreshold,
    int DirectMemberRewardInr,
    int ChildRewardInr,
    string ChildRewardTrigger,
    bool RewardSimulationEnabled,
    bool LivePaymentsEnabled,
    bool LivePayoutsEnabled,
    bool MaintenanceMode);
public record SlotRowDto(string MemberCode, string Name, int ChildCount, int OpenSlots, string Status);
public record PlacementHistoryDto(string Date, string Member, string Change, string? By, string? Reason);
public record EligibleParentDto(string MemberCode, string Name, int ChildCount, int OpenSlots);
