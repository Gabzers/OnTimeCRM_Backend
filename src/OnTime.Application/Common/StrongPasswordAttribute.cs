using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace OnTime.Application.Common;

/// <summary>
/// Enforces password complexity on registration/password-change — min 8 chars, at least one
/// uppercase, one lowercase, one digit, one special character. Never applied to LoginRequest
/// (existing passwords created before this rule must keep working).
/// </summary>
public class StrongPasswordAttribute : ValidationAttribute
{
    private static readonly Regex Pattern = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$", RegexOptions.Compiled);

    public StrongPasswordAttribute()
        : base("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number, and a special character.")
    {
    }

    public override bool IsValid(object? value) =>
        value is string s && Pattern.IsMatch(s);
}
