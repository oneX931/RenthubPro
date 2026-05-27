using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using RentHubPro.Data.Entities;

namespace RentHubPro.Services;

public static partial class PhoneNumberValidator
{
    [GeneratedRegex(@"^\+375(?:25|29|33|44)\d{7}$")]
    private static partial Regex BelarusPhoneRegex();

    public static bool IsValid(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) && BelarusPhoneRegex().IsMatch(phone.Trim());

    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var cleaned = Regex.Replace(phone, @"[\s\-\(\)]", "");
        if (cleaned.StartsWith("375")) cleaned = "+" + cleaned;
        if (cleaned.StartsWith("80")) cleaned = "+375" + cleaned[2..];
        return cleaned;
    }
}

public class BelarusPhoneUserValidator : IUserValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.PhoneNumber) && !PhoneNumberValidator.IsValid(user.PhoneNumber))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidPhoneFormat",
                Description = "Номер телефона должен быть в формате +375 (например, +375291234567)."
            }));
        }
        return Task.FromResult(IdentityResult.Success);
    }
}
