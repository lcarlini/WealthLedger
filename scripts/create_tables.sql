-- ===================================================================================
-- WealthLedger - SQLite Database Schema
-- Run this script against your SQLite database to create all required tables.
--
-- Usage:
--   sqlite3 wealthledger.db < create_tables.sql
--
-- Alternatively, if you uncomment the EnsureCreated() block in Program.cs,
-- EF Core will create these tables automatically on startup.
-- ===================================================================================

-- Enum reference (stored as INTEGER in SQLite):
--
-- AccountType:     1 = CheckingAccount, 2 = SavingsBox, 3 = FixedTerm
-- Currency:        1 = BRL, 2 = USD, 3 = EUR
-- StatementSource: 1 = Card, 2 = Checking
-- CashFlowItemType: 1 = Income, 2 = Expense, 3 = Debt, 4 = CardInstallment
-- CashFlowSource:  1 = Manual, 2 = FromCardImport

CREATE TABLE IF NOT EXISTS FinancialInstitutions (
    Id              TEXT PRIMARY KEY NOT NULL,
    Name            TEXT NOT NULL,
    Description     TEXT,
    ImageUrl        TEXT,
    CreatedDate     TEXT NOT NULL,
    UpdatedDate     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Investments (
    Id                      TEXT PRIMARY KEY NOT NULL,
    FinancialInstitutionId  TEXT NOT NULL,
    Name                    TEXT NOT NULL,
    AccountType             INTEGER NOT NULL,  -- 1=CheckingAccount, 2=SavingsBox, 3=FixedTerm
    Currency                INTEGER NOT NULL DEFAULT 1,  -- 1=BRL, 2=USD, 3=EUR
    Amount                  REAL NOT NULL DEFAULT 0,
    CdiPercentage           REAL NOT NULL DEFAULT 0,
    AnnualRatePercent       REAL,
    MaturityDate            TEXT,
    RequiresMonthlyMovement INTEGER NOT NULL DEFAULT 0,  -- 0=false, 1=true
    MonthlyMovementAmount   REAL,
    CreatedDate             TEXT NOT NULL,
    UpdatedDate             TEXT NOT NULL,
    FOREIGN KEY (FinancialInstitutionId) REFERENCES FinancialInstitutions(Id)
);

CREATE TABLE IF NOT EXISTS TaskItems (
    Id              TEXT PRIMARY KEY NOT NULL,
    InvestmentId    TEXT NOT NULL,
    Title           TEXT NOT NULL,
    Description     TEXT,
    DueYear         INTEGER NOT NULL,
    DueMonth        INTEGER NOT NULL,
    IsCompleted     INTEGER NOT NULL DEFAULT 0,  -- 0=false, 1=true
    CompletedDate   TEXT,
    CreatedDate     TEXT NOT NULL,
    UpdatedDate     TEXT NOT NULL,
    FOREIGN KEY (InvestmentId) REFERENCES Investments(Id)
);

CREATE TABLE IF NOT EXISTS StatementImports (
    Id                TEXT PRIMARY KEY NOT NULL,
    FileName          TEXT NOT NULL,
    Source            INTEGER NOT NULL,  -- 1=Card, 2=Checking
    TransactionCount  INTEGER NOT NULL DEFAULT 0,
    CreatedDate       TEXT NOT NULL,
    UpdatedDate       TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS BankTransactions (
    Id                  TEXT PRIMARY KEY NOT NULL,
    StatementImportId   TEXT NOT NULL,
    FitId               TEXT,
    Date                TEXT NOT NULL,
    Amount              REAL NOT NULL DEFAULT 0,
    Description         TEXT NOT NULL,
    Category            TEXT,
    InstallmentNumber   INTEGER,
    InstallmentTotal    INTEGER,
    CreatedDate         TEXT NOT NULL,
    UpdatedDate         TEXT NOT NULL,
    FOREIGN KEY (StatementImportId) REFERENCES StatementImports(Id)
);

CREATE TABLE IF NOT EXISTS CashFlowScheduleItems (
    Id                  TEXT PRIMARY KEY NOT NULL,
    Name                TEXT NOT NULL,
    ItemType            INTEGER NOT NULL,  -- 1=Income, 2=Expense, 3=Debt, 4=CardInstallment
    AmountPerMonth      REAL NOT NULL DEFAULT 0,
    StartYear           INTEGER NOT NULL,
    StartMonth          INTEGER NOT NULL,
    NumberOfMonths      INTEGER NOT NULL DEFAULT 1,
    Source              INTEGER NOT NULL DEFAULT 1,  -- 1=Manual, 2=FromCardImport
    BankTransactionId   TEXT,
    DisplayOrder        INTEGER NOT NULL DEFAULT 0,
    CreatedDate         TEXT NOT NULL,
    UpdatedDate         TEXT NOT NULL,
    FOREIGN KEY (BankTransactionId) REFERENCES BankTransactions(Id)
);

-- Useful indexes for common queries
CREATE INDEX IF NOT EXISTS IX_Investments_FinancialInstitutionId ON Investments(FinancialInstitutionId);
CREATE INDEX IF NOT EXISTS IX_TaskItems_InvestmentId ON TaskItems(InvestmentId);
CREATE INDEX IF NOT EXISTS IX_TaskItems_DueYearMonth ON TaskItems(DueYear, DueMonth);
CREATE INDEX IF NOT EXISTS IX_BankTransactions_StatementImportId ON BankTransactions(StatementImportId);
CREATE INDEX IF NOT EXISTS IX_BankTransactions_Date ON BankTransactions(Date);
CREATE INDEX IF NOT EXISTS IX_CashFlowScheduleItems_StartYearMonth ON CashFlowScheduleItems(StartYear, StartMonth);
