namespace UCredit.Modules.Contracts.Contracts;

public sealed record ContractOperationCatalog(string Code, string Description, int CompanyId);

public sealed record ContractCnbvCatalog(int Id, string Description, bool IsDefault);

public sealed record ContractCfdiUseCatalog(string Code, string Description);

public sealed record ContractAddressOption(int Id, int TypeCode, string? TypeDescription);

public sealed record ContractRateConfiguration(
    int RateId,
    string Description,
    bool IsFixed,
    int CurrencyCode,
    bool IsActive,
    bool HasRevision);

public sealed record ContractLateRateConfiguration(
    int RateId,
    int CalculationTypeId,
    decimal Points,
    decimal Factor,
    int CurrencyCode,
    bool IsConfigured);

public sealed record ContractCustomerContext(
    int PersonId,
    bool IsActive,
    string? TaxRegimeCode,
    bool HasAddress,
    bool HasPhone,
    bool HasAccount);

public sealed record ContractPreviewCommand(
    int PersonId,
    string OperationCode,
    decimal Capital,
    decimal DownPayment,
    decimal Iva,
    DateOnly StartDate,
    DateOnly FirstPaymentDate,
    DateOnly DisbursementRequestDate,
    int Term,
    decimal NominalAnnualRate,
    int CnbvCode,
    string CfdiUseCode,
    int AddressId);

public sealed record ContractPreviewPendingRule(string Code, string Message);

public sealed record ContractPreviewResult(
    bool CanCreate,
    string OperationCode,
    DateOnly? BusinessDate,
    decimal Capital,
    decimal DownPayment,
    decimal AmountToFinance,
    decimal InitialBalance,
    int? RateId,
    int? CnbvId,
    string? CfdiUseCode,
    int? AddressId,
    IReadOnlyList<ContractPreviewPendingRule> PendingRules);

public interface IContractFoundationRepository
{
    Task<IReadOnlyList<ContractOperationCatalog>> GetOperationsAsync(
        IReadOnlyCollection<int> companyIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractCnbvCatalog>> GetCnbvAsync(
        string operationCode,
        IReadOnlyCollection<int> companyIds,
        CancellationToken cancellationToken = default);

    Task<ContractCustomerContext?> GetCustomerAsync(
        int personId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractCfdiUseCatalog>> GetCfdiUsesAsync(
        int personId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractAddressOption>?> GetActiveAddressesAsync(
        int personId,
        CancellationToken cancellationToken = default);

    Task<ContractRateConfiguration?> GetOrdinaryRateAsync(
        string operationCode,
        int currencyCode,
        CancellationToken cancellationToken = default);

    Task<ContractLateRateConfiguration?> GetLateRateAsync(
        string operationCode,
        int currencyCode,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetBusinessDateAsync(CancellationToken cancellationToken = default);
}

public interface IContractPreviewService
{
    Task<ContractPreviewResult> PreviewAsync(
        ContractPreviewCommand command,
        IReadOnlyCollection<int> companyIds,
        CancellationToken cancellationToken = default);
}

public sealed class ContractPreviewService(IContractFoundationRepository repository)
    : IContractPreviewService
{
    public async Task<ContractPreviewResult> PreviewAsync(
        ContractPreviewCommand command,
        IReadOnlyCollection<int> companyIds,
        CancellationToken cancellationToken = default)
    {
        var pending = new List<ContractPreviewPendingRule>();
        if (command.PersonId <= 0) pending.Add(new("customer_required", "Selecciona un cliente válido."));
        if (!string.Equals(command.OperationCode, "CD", StringComparison.OrdinalIgnoreCase))
            pending.Add(new("contract_operation_not_available", "Sólo la operación CD está disponible en esta fase."));
        if (command.Capital < 0 || command.DownPayment < 0 || command.Iva < 0)
            pending.Add(new("contract_amount_invalid", "Los importes no pueden ser negativos."));
        if (command.DownPayment > command.Capital)
            pending.Add(new("contract_down_payment_invalid", "El enganche no puede superar el capital."));
        if (command.Term <= 0 || command.NominalAnnualRate < 0)
            pending.Add(new("contract_financial_values_invalid", "Plazo y tasa deben ser válidos."));

        var customer = command.PersonId > 0 ? await repository.GetCustomerAsync(command.PersonId, cancellationToken) : null;
        if (customer is null || !customer.IsActive)
            pending.Add(new("customer_inactive_or_missing", "El cliente no existe o no está activo."));

        if (customer is null || !customer.HasAddress || !customer.HasPhone || !customer.HasAccount)
            pending.Add(new("customer_profile_incomplete", "El expediente requiere domicilio, teléfono y cuenta activos."));

        var operation = companyIds.Count > 0 && string.Equals(command.OperationCode, "CD", StringComparison.OrdinalIgnoreCase)
            ? (await repository.GetOperationsAsync(companyIds, cancellationToken)).SingleOrDefault(item => item.Code == "CD")
            : null;
        if (operation is null) pending.Add(new("contract_operation_configuration_required", "La operación CD no está configurada para el tenant."));

        var cnbv = command.CnbvCode > 0 && operation is not null
            ? (await repository.GetCnbvAsync(command.OperationCode, companyIds, cancellationToken)).SingleOrDefault(item => item.Id == command.CnbvCode)
            : null;
        if (cnbv is null) pending.Add(new("contract_cnbv_invalid", "El CNBV no es válido para la operación seleccionada."));

        var cfdi = customer is not null
            ? (await repository.GetCfdiUsesAsync(command.PersonId, cancellationToken)).SingleOrDefault(item => string.Equals(item.Code, command.CfdiUseCode, StringComparison.Ordinal))
            : null;
        if (cfdi is null) pending.Add(new("contract_cfdi_invalid", "El Uso CFDI no es compatible con el cliente."));

        var addresses = command.PersonId > 0 ? await repository.GetActiveAddressesAsync(command.PersonId, cancellationToken) : null;
        if (addresses is null || addresses.All(item => item.Id != command.AddressId))
            pending.Add(new("contract_address_invalid", "El domicilio no pertenece al cliente o no está activo."));

        var rate = operation is not null ? await repository.GetOrdinaryRateAsync(command.OperationCode, 1, cancellationToken) : null;
        if (rate is null) pending.Add(new("contract_rate_configuration_required", "No existe una tasa fija ordinaria activa en pesos."));

        var lateRate = operation is not null ? await repository.GetLateRateAsync(command.OperationCode, 1, cancellationToken) : null;
        if (lateRate is null) pending.Add(new("contract_late_rate_configuration_required", "No existe configuración moratoria activa para CD en pesos."));

        var businessDate = await repository.GetBusinessDateAsync(cancellationToken);
        if (businessDate is null) pending.Add(new("contract_business_date_required", "No existe una fecha operativa única válida."));

        var amountToFinance = command.Capital - command.DownPayment;
        return new ContractPreviewResult(
            pending.Count == 0,
            "CD",
            businessDate,
            command.Capital,
            command.DownPayment,
            amountToFinance,
            amountToFinance,
            rate?.RateId,
            cnbv?.Id,
            cfdi?.Code,
            addresses?.FirstOrDefault(item => item.Id == command.AddressId)?.Id,
            pending);
    }
}
