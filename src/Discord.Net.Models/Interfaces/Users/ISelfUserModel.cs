namespace Discord.Models;

[ModelEquality]
public partial interface ISelfUserModel : IUserModel
{
    int? PremiumType { get; }
    string? Email { get; }
    bool? Verified { get; }
    string? Locale { get; }
    bool? MFAEnabled { get; }
}
