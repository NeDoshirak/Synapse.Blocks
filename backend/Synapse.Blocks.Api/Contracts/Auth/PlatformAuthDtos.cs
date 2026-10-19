namespace Synapse.Blocks.Api.Contracts.Auth;

public sealed record AuthStepDto(string NextStep);

public sealed record AuthenticatorSetupDto(string SharedKey, string AuthenticatorUri);

public sealed record AuthenticatorCodeRequest(string Code);

public sealed record RecoveryCodeSignInRequest(string Code);

public sealed record RecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

public sealed record PasswordResetCompleteRequest(string Email, string Token, string NewPassword);

public sealed record InvitationRequest(string Email);

public sealed record AcceptInvitationRequest(
    string Token,
    string Password,
    string FirstName,
    string LastName,
    string? TeacherDescription);

public sealed record InvitationCreatedDto(string InvitationUrl, DateTimeOffset ExpiresAt);

public sealed record PendingInvitationDto(Guid Id, string Email, DateTimeOffset ExpiresAt);

public sealed record TeacherAccountDto(
    string Id,
    string Email,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? TeacherDescription,
    bool IsActive,
    bool TwoFactorEnabled);

public sealed record PasswordResetLinkDto(string ResetUrl, DateTimeOffset ExpiresAt);
