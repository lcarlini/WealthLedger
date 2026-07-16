# Plan: Rates, Calculator, OFX, Analytics

## 1. Market data (exchange rates + indices)
- **Backend**: Service that fetches USD/BRL, EUR/BRL from BCB PTAX; Selic and IPCA from BCB Expectativas. Cache: 30 min for FX, 24h for indices. On fetch failure, keep last value.
- **Dashboard**: Show cards with current rates and indices.

## 2. Investments in USD/EUR
- Add **Currency** (BRL, USD, EUR) and optional **AnnualRate** (e.g. 4% a.a.) for non-CDI products. CDI-based investments remain BRL.

## 3. OFX import (card + checking)
- **Entities**: BankStatement (or ImportBatch), Transaction (amount, date, description, category, installment info).
- **API**: Upload OFX file, parse, store transactions. Future: parcelas (installments), categorization (rules first; optional local AI later).
- **UI**: Aba "Import" / "Statements" with upload and list of transactions; categorization can be manual or rule-based first.

## 4. Investment analytics
- Projected gains per investment (CDI % or annual rate, time). Where losing (e.g. below inflation). Optimization hints (e.g. move to higher CDI).

## 5. Calculator tab
- Yield: initial amount, rate, time → final amount. Options: deduct IR, deduct inflation.
- Poupança: formula (Selic-based). Comparison: Poupança vs CDI vs fixed rate.
- Patrimony projection: current portfolio + monthly contributions, growth rate → future value over years.

## 6. Categorization & AI
- Start with **rule-based** (keywords, fixed vs variable). Optional later: **local AI** (Ollama, etc.) for categorizing descriptions. Project calls local endpoint; no cloud required.

---

## Implemented (this session)
- **Market data**: Backend `MarketDataService` (BCB PTAX + Expectativas), cache 30min FX / 24h indices, dashboard widgets (USD, EUR, Selic, IPCA, Poupanca).
- **Investments**: Currency (BRL/USD/EUR) + optional AnnualRatePercent for USD/EUR; list and form updated.
- **Calculator**: Tab Yield (amount, rate, custom years, deduct IR/inflation), tab Poupança vs CDI vs Fixed, tab Patrimony projection with chart.
- **Analytics**: Projected gains over a user-selected horizon from today (3 years by default), below-inflation warning, optimization hints.
- **OFX**: Entities `StatementImport`, `BankTransaction`; OFX 2.x XML parser; POST/GET imports, GET transactions; frontend upload + list + expand to see transactions.

## Later: Variable-income investments
- **AccountType** extended with Stock, FII, ETF, InvestmentFund, BDR, Crypto.
- Optional fields: Ticker, Quantity, AveragePrice; Amount remains current market value.
- Yield projections (dashboard / analytics) treat variable-income as mark-to-market (no CDI/annual-rate compounding).
- Existing cash / fixed-income types and CSV columns remain fully compatible.
- **Live quotes**: `POST /api/investments/refresh-prices` uses brapi.dev (B3 equities/FIIs/ETFs/BDRs) and CoinGecko (crypto). Sets `Amount = Quantity × price` (FX via existing market data when needed). Optional `StockQuotes:BrapiToken` in appsettings for non-sandbox tickers.

## Portfolio hub (competitor-inspired)
Inspired by Kinvo / Status Invest / Empower / Sharesight-style workflows, adapted to WealthLedger’s local-first model:
- Unrealized P&L from quantity × average price vs mark-to-market amount
- Asset allocation (type / currency / institution) with rebalancing suggestions
- Benchmark comparison (live CDI/IPCA/Poupança + illustrative Ibov/S&P)
- Financial health score + portfolio score (diversification, risk, long-term fit)
- Goals (net worth, emergency, retirement) with progress & projected amount
- Investment calendar (maturities, monthly movements, passive income)
- Passive income ledger (dividends, interest, JCP, FII yields)
- Watchlist with optional price alerts (brapi/CoinGecko)
- Net-worth snapshots + timeline chart
- Full JSON export/import (`/api/portfolio/export-json`, `import-json`)

## Investment comparison calculator (Calculator tab)
- Side-by-side scenarios for Poupança, CDB, LCI, LCA, Tesouro, fixed term, stocks, FIIs, ETFs, mutual funds, BDRs, crypto.
- Brazilian IR rules: regressive table for CDB/Tesouro; exemptions for LCI/LCA/Poupança; flat capital-gains rates for variable income.
- Gross/net/real returns with optional fees, monthly contributions, and IPCA adjustment.
- Any custom projection horizon in years (3 years by default), dynamically anchored to the current date, plus ranking, charts, and personalized tips.
