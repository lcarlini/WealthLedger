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
- **Calculator**: Tab Yield (amount, rate, months, deduct IR/inflation), tab Poupança vs CDI vs Fixed, tab Patrimony projection with chart.
- **Analytics**: Projected gains in 12 months per investment, below-inflation warning, optimization hints.
- **OFX**: Entities `StatementImport`, `BankTransaction`; OFX 2.x XML parser; POST/GET imports, GET transactions; frontend upload + list + expand to see transactions.
