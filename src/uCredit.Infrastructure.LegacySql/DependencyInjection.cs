using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Infrastructure.LegacySql.Contracts;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Contracts.Contracts;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql;

public static class DependencyInjection
{
    public static IServiceCollection AddLegacySql(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LegacySqlOptions>()
            .Bind(configuration.GetSection(LegacySqlOptions.SectionName))
            .Validate(options => options.CommandTimeoutSeconds is > 0 and <= 300,
                "Command timeout must be between 1 and 300 seconds.");

        services.AddScoped<IContractReadRepository, LegacyContractReadRepository>();
        services.AddScoped<IContractAmortizationReadRepository, LegacyContractAmortizationReadRepository>();
        services.AddScoped<ICustomerReadRepository, LegacyCustomerReadRepository>();
        return services;
    }
}

