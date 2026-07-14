using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Domain;
using WealthLedger.Infrastructure.Repositories;
using WealthLedger.Infrastructure.Services;
using WealthLedger.Infrastructure.Validators;
using Microsoft.Extensions.DependencyInjection;
using WealthLedger.Infrastructure.Data;

namespace WealthLedger.Infrastructure.IoC;

public static class ServiceRegistration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        // ===================================================================================
        // Repositories are now configured to use EfRepository with SQLite persistence
        // instead of the in-memory implementation.
        // ===================================================================================

        // --- InMemory repositories (previous default) ---
        // services.AddSingleton<IRepository<FinancialInstitution>, InMemoryRepository<FinancialInstitution>>();
        // services.AddSingleton<IRepository<Investment>, InMemoryRepository<Investment>>();
        // services.AddSingleton<IRepository<TaskItem>, InMemoryRepository<TaskItem>>();
        // services.AddSingleton<IRepository<StatementImport>, InMemoryRepository<StatementImport>>();
        // services.AddSingleton<IRepository<BankTransaction>, InMemoryRepository<BankTransaction>>();
        // services.AddSingleton<IRepository<CashFlowScheduleItem>, InMemoryRepository<CashFlowScheduleItem>>();
        // services.AddSingleton<IRepository<IncomeProfile>, InMemoryRepository<IncomeProfile>>();
        // services.AddSingleton<IRepository<ExtraIncome>, InMemoryRepository<ExtraIncome>>();
        // services.AddSingleton<IRepository<BusinessDayOverride>, InMemoryRepository<BusinessDayOverride>>();

        // --- EfRepository (SQLite) repositories (active) ---
        services.AddScoped<IRepository<FinancialInstitution>, EfRepository<FinancialInstitution>>();
        services.AddScoped<IRepository<Investment>, EfRepository<Investment>>();
        services.AddScoped<IRepository<TaskItem>, EfRepository<TaskItem>>();
        services.AddScoped<IRepository<StatementImport>, EfRepository<StatementImport>>();
        services.AddScoped<IRepository<BankTransaction>, EfRepository<BankTransaction>>();
        services.AddScoped<IRepository<CashFlowScheduleItem>, EfRepository<CashFlowScheduleItem>>();
        services.AddScoped<IRepository<IncomeProfile>, EfRepository<IncomeProfile>>();
        services.AddScoped<IRepository<ExtraIncome>, EfRepository<ExtraIncome>>();
        services.AddScoped<IRepository<BusinessDayOverride>, EfRepository<BusinessDayOverride>>();

        // ===================================================================================

        services.AddTransient<IEntityService<FinancialInstitution>, EntityService<FinancialInstitution>>();
        services.AddTransient<IPayloadValidator<FinancialInstitutionRequestBody>, FinancialInstitutionValidator>();

        services.AddTransient<InvestmentService>();
        services.AddTransient<IInvestmentService>(sp => sp.GetRequiredService<InvestmentService>());
        services.AddTransient<IEntityService<Investment>>(sp => sp.GetRequiredService<InvestmentService>());
        services.AddTransient<IPayloadValidator<InvestmentRequestBody>, InvestmentValidator>();

        services.AddTransient<ITaskService, TaskService>();

        services.AddTransient<IDashboardService, DashboardService>();
        services.AddTransient<IMarketDataService, MarketDataService>();

        services.AddTransient<IOfxImportService, OfxImportService>();

        services.AddTransient<ICashFlowScheduleService, CashFlowScheduleService>();
        services.AddTransient<IPayloadValidator<CashFlowScheduleItemRequestBody>, CashFlowScheduleItemValidator>();

        services.AddTransient<IIncomeService, IncomeService>();

        services.AddAutoMapper(typeof(ServiceRegistration).Assembly);

        return services;
    }
}
