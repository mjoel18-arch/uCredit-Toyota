using System.Net.Mail;

namespace UCredit.Modules.Customers.Customers;

public enum CustomerCreatePersonality
{
    Individual = 1,
    IndividualBusiness = 2,
    Moral = 20
}

public sealed record CustomerCreateCommand(
    CustomerCreatePersonality LegalPersonality,
    string Rfc,
    string? FirstName,
    string? PaternalSurname,
    string? MaternalSurname,
    string? LegalName,
    string? CapitalRegime,
    DateOnly ConstitutionOrBirthDate,
    int CountryCode,
    int GroupCode,
    int RiskCode,
    int ContactFormCode,
    int TaxRegimeCode,
    int AddressTypeCode,
    string PostalCode,
    string State,
    string City,
    string Municipality,
    string Neighborhood,
    string StreetAndNumber,
    string ExteriorNumber,
    string? InteriorNumber,
    string? AddressReference,
    string? AddressSchedule,
    int AddressStatusCode,
    int PhoneTypeCode,
    string AreaCode,
    string PhoneNumber,
    string? PhoneExtension,
    string? PhoneContact,
    string EmailContact,
    string Email,
    IReadOnlyList<int> EmailUsageCodes);

public sealed record CustomerCreateResult(int PersonId);

public enum PepCheckStatus
{
    NoMatch,
    Match,
    Unavailable
}

public sealed record PepCheckResult(PepCheckStatus Status);

public interface IPersonPepChecker
{
    Task<PepCheckResult> CheckAsync(CustomerCreateCommand command, CancellationToken cancellationToken = default);
}

public sealed class CustomerCreationOptions
{
    public bool RequirePepCheck { get; init; } = true;
}

public interface ICustomerWriteRepository
{
    Task<CustomerCreateResult> CreateAsync(CustomerCreateCommand command, string legacyUserCode, CancellationToken cancellationToken = default);
}

public static class CustomerCreateValidator
{
    public static Dictionary<string, string[]> Validate(CustomerCreateCommand command)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(string key, string message)
        {
            if (errors.TryGetValue(key, out var list))
            {
                list.Add(message);
            }
            else
            {
                errors[key] = [message];
            }
        }

        if (!Enum.IsDefined(command.LegalPersonality)) Add("legalPersonality", "The legal personality is not supported.");
        if (string.IsNullOrWhiteSpace(command.Rfc) || command.Rfc.Length > 13) Add("rfc", "RFC is required and must not exceed 13 characters.");
        if (command.ConstitutionOrBirthDate > DateOnly.FromDateTime(DateTime.UtcNow)) Add("constitutionOrBirthDate", "The date cannot be in the future.");
        if (command.CountryCode <= 0) Add("countryCode", "Country is required.");
        if (command.GroupCode <= 0) Add("groupCode", "Group is required.");
        if (command.RiskCode <= 0) Add("riskCode", "Risk is required.");
        if (command.ContactFormCode <= 0) Add("contactFormCode", "Contact form is required.");
        if (command.TaxRegimeCode <= 0) Add("taxRegimeCode", "Tax regime is required.");
        if (command.AddressTypeCode is < 1 or > 4) Add("addressTypeCode", "Only address types 1 through 4 are supported in version 1.");
        Require(command.PostalCode, 10, "postalCode", Add);
        Require(command.State, 100, "state", Add);
        Require(command.City, 100, "city", Add);
        Require(command.Municipality, 100, "municipality", Add);
        Require(command.Neighborhood, 100, "neighborhood", Add);
        Require(command.StreetAndNumber, 200, "streetAndNumber", Add);
        Require(command.ExteriorNumber, 20, "exteriorNumber", Add);
        if (command.AddressStatusCode <= 0) Add("addressStatusCode", "Address status is required.");
        if (command.PhoneTypeCode <= 0) Add("phoneTypeCode", "Phone type is required.");
        Require(command.AreaCode, 10, "areaCode", Add);
        Require(command.PhoneNumber, 30, "phoneNumber", Add);
        Require(command.EmailContact, 250, "emailContact", Add);
        Require(command.Email, 250, "email", Add);
        if (!MailAddress.TryCreate(command.Email, out _)) Add("email", "Email format is invalid.");
        if (command.EmailUsageCodes.Count == 0 || command.EmailUsageCodes.Any(code => code is < 1 or > 3) || command.EmailUsageCodes.Distinct().Count() != command.EmailUsageCodes.Count)
            Add("emailUsageCodes", "Email usages must contain one or more distinct values from 1 through 3.");

        if (command.LegalPersonality == CustomerCreatePersonality.Moral)
        {
            Require(command.LegalName, 200, "legalName", Add);
            Require(command.CapitalRegime, 200, "capitalRegime", Add);
        }
        else
        {
            Require(command.FirstName, 100, "firstName", Add);
            if (string.IsNullOrWhiteSpace(command.PaternalSurname) && string.IsNullOrWhiteSpace(command.MaternalSurname)) Add("paternalSurname", "At least one surname is required.");
            RequireOptional(command.PaternalSurname, 100, "paternalSurname", Add);
            RequireOptional(command.MaternalSurname, 100, "maternalSurname", Add);
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void Require(string? value, int max, string key, Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) add(key, $"{key} is required and must not exceed {max} characters.");
    }

    private static void RequireOptional(string? value, int max, string key, Action<string, string> add)
    {
        if (value is not null && value.Length > max) add(key, $"{key} must not exceed {max} characters.");
    }
}
