using WealthLedger.Contracts.Domain;
using Microsoft.EntityFrameworkCore;

namespace WealthLedger.Infrastructure.Data;

public class WealthLedgerDbContext : DbContext
{
    public WealthLedgerDbContext(DbContextOptions<WealthLedgerDbContext> options) : base(options) { }

    public DbSet<FinancialInstitution> FinancialInstitutions => Set<FinancialInstitution>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<StatementImport> StatementImports => Set<StatementImport>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<CashFlowScheduleItem> CashFlowScheduleItems => Set<CashFlowScheduleItem>();
    public DbSet<IncomeProfile> IncomeProfiles => Set<IncomeProfile>();
    public DbSet<ExtraIncome> ExtraIncomes => Set<ExtraIncome>();
    public DbSet<BusinessDayOverride> BusinessDayOverrides => Set<BusinessDayOverride>();
    public DbSet<PassiveIncome> PassiveIncomes => Set<PassiveIncome>();
    public DbSet<InvestmentGoal> InvestmentGoals => Set<InvestmentGoal>();
    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FinancialInstitution>(e =>
        {
            e.ToTable("FinancialInstitutions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Investment>(e =>
        {
            e.ToTable("Investments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Amount).HasColumnType("REAL");
            e.Property(x => x.CdiPercentage).HasColumnType("REAL");
            e.Property(x => x.AnnualRatePercent).HasColumnType("REAL");
            e.Property(x => x.MonthlyMovementAmount).HasColumnType("REAL");
            e.Property(x => x.Ticker).HasMaxLength(30);
            e.Property(x => x.Quantity).HasColumnType("REAL");
            e.Property(x => x.AveragePrice).HasColumnType("REAL");
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("TaskItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<StatementImport>(e =>
        {
            e.ToTable("StatementImports");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(300);
        });

        modelBuilder.Entity<BankTransaction>(e =>
        {
            e.ToTable("BankTransactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).IsRequired().HasMaxLength(500);
            e.Property(x => x.FitId).HasMaxLength(100);
            e.Property(x => x.TransactionType).HasMaxLength(20);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Amount).HasColumnType("REAL");
        });

        modelBuilder.Entity<CashFlowScheduleItem>(e =>
        {
            e.ToTable("CashFlowScheduleItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.AmountPerMonth).HasColumnType("REAL");
        });

        modelBuilder.Entity<IncomeProfile>(e =>
        {
            e.ToTable("IncomeProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.HourlyRateUsd).HasColumnType("REAL");
            e.Property(x => x.HoursPerDay).HasDefaultValue(8);
            e.Property(x => x.UsdBrlRate).HasColumnType("REAL");
            e.Property(x => x.TaxPercent).HasColumnType("REAL");
        });

        modelBuilder.Entity<ExtraIncome>(e =>
        {
            e.ToTable("ExtraIncomes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("REAL");
            e.Property(x => x.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<BusinessDayOverride>(e =>
        {
            e.ToTable("BusinessDayOverrides");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Year, x.Month }).IsUnique();
        });

        modelBuilder.Entity<PassiveIncome>(e =>
        {
            e.ToTable("PassiveIncomes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Amount).HasColumnType("REAL");
            e.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<InvestmentGoal>(e =>
        {
            e.ToTable("InvestmentGoals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.TargetAmount).HasColumnType("REAL");
            e.Property(x => x.CurrentAmount).HasColumnType("REAL");
            e.Property(x => x.MonthlyContribution).HasColumnType("REAL");
            e.Property(x => x.ExpectedAnnualReturnPercent).HasColumnType("REAL");
            e.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<PortfolioSnapshot>(e =>
        {
            e.ToTable("PortfolioSnapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalAmountBrl).HasColumnType("REAL");
            e.Property(x => x.CashAmountBrl).HasColumnType("REAL");
            e.Property(x => x.FixedIncomeAmountBrl).HasColumnType("REAL");
            e.Property(x => x.VariableIncomeAmountBrl).HasColumnType("REAL");
            e.Property(x => x.UnrealizedGainBrl).HasColumnType("REAL");
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.SnapshotDate);
        });

        modelBuilder.Entity<WatchlistItem>(e =>
        {
            e.ToTable("WatchlistItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Ticker).IsRequired().HasMaxLength(30);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.TargetPrice).HasColumnType("REAL");
            e.Property(x => x.AlertAbove).HasColumnType("REAL");
            e.Property(x => x.AlertBelow).HasColumnType("REAL");
            e.Property(x => x.Notes).HasMaxLength(500);
        });
    }
}
