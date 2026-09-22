using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Infrastructure.LegacySql.Contracts;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Contracts.Contracts;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql;

public static class DependencyInjection
{
    public static IServiceCollection AddLegacySql(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LegacySqlOptions>()
            .Bind(configuration.GetSection(LegacySqlOptions.SectionName))
            .Configure(options =>
            {
                if (bool.TryParse(Environment.GetEnvironmentVariable("UCREDIT_ALLOW_LEGACY_WRITE_TESTS"), out var allow))
                    options.AllowLegacyWriteTests = allow;
                var database = Environment.GetEnvironmentVariable("UCREDIT_LEGACY_WRITE_TEST_DATABASE");
                if (database is not null)
                    options.LegacyWriteTestDatabase = database;
            })
            .Validate(options => options.CommandTimeoutSeconds is > 0 and <= 300, "Command timeout must be between 1 and 300 seconds.");
        services.AddScoped<IContractReadRepository, LegacyContractReadRepository>();
        services.AddScoped<IContractAmortizationReadRepository, LegacyContractAmortizationReadRepository>();
        services.AddScoped<ICustomerReadRepository, LegacyCustomerReadRepository>();
        services.AddScoped<ICustomerAddressReadRepository, LegacyCustomerAddressReadRepository>();
        services.AddScoped<ICustomerPhoneReadRepository, LegacyCustomerPhoneReadRepository>();
        services.AddScoped<ICustomerAccountReadRepository, LegacyCustomerAccountReadRepository>();
        services.AddScoped<ICustomerBankReadRepository, LegacyCustomerBankReadRepository>();
        services.AddScoped<ICustomerRoleCatalogRepository, LegacyCustomerRoleCatalogRepository>();
        services.AddScoped<LegacyCustomerEmailReadRepository>();
        services.AddScoped<ICustomerEmailReadRepository>(provider => provider.GetRequiredService<LegacyCustomerEmailReadRepository>());
        services.AddScoped<ICustomerEmailUsageRepository>(provider => provider.GetRequiredService<LegacyCustomerEmailReadRepository>());
        services.AddScoped<ICustomerEmailWriteRepository, LegacyCustomerEmailWriteRepository>();
        services.AddScoped<ICustomerProfileReadinessRepository, LegacyCustomerProfileReadinessRepository>();
        services.AddScoped<ICustomerProfileReadinessService, CustomerProfileReadinessService>();
        services.AddScoped<LegacyCustomerWriteRepository>();
        services.AddScoped<ICustomerWriteRepository>(provider => provider.GetRequiredService<LegacyCustomerWriteRepository>());
        services.AddScoped<ICustomerAddressWriteRepository>(provider => provider.GetRequiredService<LegacyCustomerWriteRepository>());
        services.AddScoped<ICustomerPhoneWriteRepository, LegacyCustomerPhoneWriteRepository>();
        services.AddScoped<ICustomerAccountWriteRepository, LegacyCustomerAccountWriteRepository>();
        return services;
    }
}
