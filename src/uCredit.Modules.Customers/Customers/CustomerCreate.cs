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
    string TaxRegimeCode,
    int PhoneTypeCode,
    string AreaCode,
    string PhoneNumber,
    string? PhoneExtension,
    string? PhoneContact);

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

public sealed class CustomerCreateValidationException(string message) : Exception(message);

public interface ICustomerWriteRepository
{
    Task<CustomerCreateResult> CreateAsync(CustomerCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default);
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
        if (string.IsNullOrWhiteSpace(command.TaxRegimeCode) || command.TaxRegimeCode.Trim().Length > 20 || !int.TryParse(command.TaxRegimeCode.Trim(), out _)) Add("taxRegimeCode", "Tax regime must be a valid SAT key of at most 20 characters.");
        if (command.PhoneTypeCode <= 0) Add("phoneTypeCode", "Phone type is required.");
        Require(command.AreaCode, 10, "areaCode", Add);
        Require(command.PhoneNumber, 30, "phoneNumber", Add);
        if (command.LegalPersonality == CustomerCreatePersonality.Moral)
        {
            Require(command.LegalName, 200, "legalName", Add);
            Add("capitalRegime", "The capital regime catalog is not configured for customer creation.");
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
