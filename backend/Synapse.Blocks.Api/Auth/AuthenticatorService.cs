using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Synapse.Blocks.Api.Contracts.Auth;

namespace Synapse.Blocks.Api.Auth;

public sealed class AuthenticatorService(UserManager<ApplicationUser> users)
{
    private const string Issuer = "ФВС/Код";
    private static readonly Regex AuthenticatorCodePattern = new("^[0-9]{6}$", RegexOptions.Compiled);

    public async Task<AuthenticatorSetupDto?> GetSetupAsync(ApplicationUser user)
    {
        if (!user.IsActive || user.TwoFactorEnabled) return null;

        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            var reset = await users.ResetAuthenticatorKeyAsync(user);
            if (!reset.Succeeded) throw new InvalidOperationException("Could not initialize the authenticator key.");
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Could not initialize the authenticator key.");
        var accountName = user.Email ?? user.UserName ?? user.Id;
        var label = Uri.EscapeDataString($"{Issuer}:{accountName}");
        var issuer = Uri.EscapeDataString(Issuer);
        var uri = $"otpauth://totp/{label}?secret={Uri.EscapeDataString(key)}&issuer={issuer}&digits=6&period=30";
        return new AuthenticatorSetupDto(key, uri);
    }

    public async Task<IReadOnlyList<string>?> ConfirmAsync(ApplicationUser user, string? code)
    {
        var normalizedCode = code?.Trim();
        if (!user.IsActive || user.TwoFactorEnabled || !IsAuthenticatorCode(normalizedCode)) return null;
        if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, normalizedCode!)) return null;

        var enabled = await users.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded) throw new InvalidOperationException("Could not enable authenticator-based two-factor authentication.");

        var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes?.ToArray() ?? [];
    }

    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(ApplicationUser user)
    {
        if (!user.IsActive || !user.TwoFactorEnabled)
            throw new InvalidOperationException("Recovery codes can only be regenerated for an active MFA-enabled account.");
        var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes?.ToArray() ?? [];
    }

    public static bool IsAuthenticatorCode(string? code)
        => code is not null && AuthenticatorCodePattern.IsMatch(code);
}
