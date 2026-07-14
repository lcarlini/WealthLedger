// TODO: SQLLITE - This DbContext is used when switching from InMemory to SQLite.
// It is referenced in Services.cs and Program.cs (both commented out by default).

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
    }
}
