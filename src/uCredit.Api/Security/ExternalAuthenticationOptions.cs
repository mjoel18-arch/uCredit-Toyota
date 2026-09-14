using System.ComponentModel.DataAnnotations;

namespace UCredit.Api.Security;

public sealed class ExternalAuthenticationOptions
{
    public const string SectionName = "Authentication";

    [Required, Url]
    public string Authority { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;
}