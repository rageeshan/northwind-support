using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITicketEscalationRepository, TicketEscalationRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();
        services.AddScoped<IAgentTriageQueries, AgentTriageQueries>();

        services.AddScoped<SupportDbSeeder>();

        // Bind the SLA options from configuration (BR-6: all window values come from config).
        services.AddScoped<SlaOptions>(_ =>
        {
            var section = configuration.GetSection(SlaOptions.Section);
            var windows = new Dictionary<string, double>();
            foreach (var child in section.GetSection("BaseWindowHours").GetChildren())
            {
                if (double.TryParse(child.Value, out var h))
                    windows[child.Key] = h;
            }
            return new SlaOptions
            {
                BaseWindowHours = windows,
                PremiumCustomerMultiplier = double.TryParse(section["PremiumCustomerMultiplier"], out var m) ? m : 0.5,
                MinimumWindowHours = double.TryParse(section["MinimumWindowHours"], out var min) ? min : 1,
                AtRiskThresholdPercent = double.TryParse(section["AtRiskThresholdPercent"], out var t) ? t : 25
            };
        });

        return services;
    }
}
