# Control / Simulation (Cash flow schedule)

The system generates a **cash flow simulation**: months as columns, rows as items (income, expenses, debts, card installments), with per-month totals and a running accumulated balance.

## Data model

- **CashFlowScheduleItem**: One row in the simulation.
  - **Name** (e.g. "Dentist", "Rent", "Credit card")
  - **ItemType**: Income, Expense, Debt, CardInstallment
  - **AmountPerMonth**: Value per month (negative for expenses/debts, positive for income)
  - **StartYear**, **StartMonth**: First month the item applies
  - **NumberOfMonths**: How many months (e.g. 4 for "dentist for 4 months")
  - **Source**: Manual or FromCardImport (when added from OFX card installments)
  - **BankTransactionId**: Optional link to the OFX transaction when from card

## Usage

1. **Control** page (menu): Three tabs.
   - **Simulation**: Table with months as columns, each schedule item as a row, totals and accumulated. You can change "From year/month" and "Months" (6/12/24). **Export CSV** downloads a snapshot of the current simulation.
   - **Add item**: Add debts/expenses/income manually (name, type, amount per month, start year/month, number of months).
   - **From card (OFX)**: After importing card OFX with installment data, future installments are proposed here. You choose which to **Add to schedule**; they appear as rows in the simulation.

2. **Import OFX** (card or checking) first so that card installments are available to include in the simulation.

## API

- `GET /api/cashflow-schedule` – list all schedule items
- `POST /api/cashflow-schedule` – create item (manual)
- `PUT /api/cashflow-schedule/{id}` – update item
- `DELETE /api/cashflow-schedule/{id}` – remove item
- `GET /api/cashflow-schedule/simulation?fromYear=&fromMonth=&monthCount=` – matrix (rows, columns, totals, accumulated)
- `GET /api/cashflow-schedule/proposed-card-installments` – future installments from OFX
- `POST /api/cashflow-schedule/from-card/{bankTransactionId}` – add one proposed installment to the schedule
- `GET /api/cashflow-schedule/export-csv?fromYear=&fromMonth=&monthCount=` – download the simulation as CSV

## Exporting

Use **Export CSV** on the Simulation tab to download the current cash flow projection at any time. Exported files are for your own records and are never committed to the repository.
