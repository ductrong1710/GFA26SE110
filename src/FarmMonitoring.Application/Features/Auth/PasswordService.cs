using System.Security.Cryptography;
using System.Text;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Application.Features.Alerts;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Auth;

public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed class PasswordRecoveryService(IAuthRepository users, IPasswordRecoveryRepository recovery,
    IPasswordService passwords, IEmailSender sender, EmailDeliverySettings email, IPasswordResetQueue queue)
{
    private static void NewPassword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 12 or > 1024)
            throw new ValidationException("New password must contain 12 to 1024 characters.");
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public Task ForgotAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 255 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email.Trim()))
            throw new ValidationException("A valid email address is required.");
        ct.ThrowIfCancellationRequested();
        queue.Enqueue(request.Email.Trim().ToLowerInvariant());
        return Task.CompletedTask;
    }

    // Called in a background scope: HTTP timing never depends on account eligibility or SMTP.
    public async Task DeliverResetAsync(string normalizedEmail, CancellationToken ct)
    {
        var user = await users.GetUserByEmailAsync(normalizedEmail, ct);
        if (user is null || !user.IsActive || !email.Enabled) return;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (!await recovery.IssueAsync(user, Hash(token), ct)) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(email.TimeoutSeconds));
        await sender.SendAsync(new(0, user.Email, "Reset your Farm Monitoring password",
            "Use this one-time token to reset your password within 15 minutes. If you did not request this, ignore this email.\n" + token), timeout.Token);
    }

    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        NewPassword(request.NewPassword);
        if (request.Token is null || request.Token.Length != 64 || !request.Token.All(Uri.IsHexDigit))
            throw new ValidationException("Invalid or expired reset token.");
        var hash = Hash(request.Token);
        var user = await recovery.FindResetAsync(hash, ct);
        if (user is null || !await recovery.ReplaceAsync(user, passwords.Hash(user, request.NewPassword), hash, ct))
            throw new ValidationException("Invalid or expired reset token.");
    }

    public async Task ChangeAsync(int userId, ChangePasswordRequest request, CancellationToken ct)
    {
        NewPassword(request.NewPassword);
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 1024)
            throw new ValidationException("Current password is required.");
        var user = await users.GetUserByIdAsync(userId, ct);
        if (user is null || !user.IsActive || !passwords.Verify(user, request.CurrentPassword).Succeeded ||
            !await recovery.ReplaceAsync(user, passwords.Hash(user, request.NewPassword), null, ct))
            throw new ValidationException("Current password is invalid or the account has changed.");
    }
}
