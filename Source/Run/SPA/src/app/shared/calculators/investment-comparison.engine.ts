import {
  ComparisonGlobalSettings,
  ComparisonProductLabels,
  ComparisonProductType,
  ComparisonRunResult,
  ComparisonScenarioInput,
  ComparisonScenarioResult,
  MonthlyProjectionPoint,
} from './investment-comparison.models';

/** Brazilian regressive IR on fixed-income gains (CDB, Tesouro, etc.). */
function regressiveIrRate(holdingDays: number): number {
  if (holdingDays <= 180) return 22.5;
  if (holdingDays <= 360) return 20;
  if (holdingDays <= 720) return 17.5;
  return 15;
}

function isTaxExempt(productType: ComparisonProductType): boolean {
  return productType === ComparisonProductType.Lci
    || productType === ComparisonProductType.Lca
    || productType === ComparisonProductType.Savings;
}

function usesRegressiveIr(productType: ComparisonProductType): boolean {
  return productType === ComparisonProductType.Cdb
    || productType === ComparisonProductType.TreasuryBond
    || productType === ComparisonProductType.FixedTerm
    || productType === ComparisonProductType.CheckingAccount;
}

function flatCapitalGainsRate(productType: ComparisonProductType): number {
  switch (productType) {
    case ComparisonProductType.Fii:
      return 20;
    case ComparisonProductType.Stock:
    case ComparisonProductType.Etf:
    case ComparisonProductType.Bdr:
    case ComparisonProductType.InvestmentFund:
    case ComparisonProductType.Crypto:
      return 15;
    default:
      return 15;
  }
}

export function resolveEffectiveAnnualRate(
  scenario: ComparisonScenarioInput,
  settings: ComparisonGlobalSettings,
): number {
  switch (scenario.rateMode) {
    case 'poupanca':
      return settings.poupancaPercent;
    case 'cdi':
      return settings.cdiBasePercent * (scenario.cdiPercent / 100);
    case 'fixed':
    default:
      return scenario.annualRatePercent;
  }
}

function calculateIncomeTax(
  productType: ComparisonProductType,
  grossGain: number,
  months: number,
  deductTaxes: boolean,
): number {
  if (!deductTaxes || grossGain <= 0)
    return 0;

  if (isTaxExempt(productType))
    return 0;

  if (usesRegressiveIr(productType)) {
    const holdingDays = Math.round(months * 30.4375);
    return grossGain * (regressiveIrRate(holdingDays) / 100);
  }

  return grossGain * (flatCapitalGainsRate(productType) / 100);
}

function projectMonthly(
  scenario: ComparisonScenarioInput,
  settings: ComparisonGlobalSettings,
  months: number,
): { points: MonthlyProjectionPoint[]; totalContributed: number; grossFinal: number; fees: number } {
  const annualRate = resolveEffectiveAnnualRate(scenario, settings);
  const monthlyRate = annualRate / 100 / 12;
  const monthlyAdminFee = settings.deductFees ? scenario.adminFeePercentPerYear / 100 / 12 : 0;
  const monthlyCustodyFee = settings.deductFees ? scenario.custodyFeePercentPerYear / 100 / 12 : 0;
  const totalFeeRate = monthlyAdminFee + monthlyCustodyFee;

  let balance = scenario.initialAmount;
  let totalContributed = scenario.initialAmount;
  let fees = 0;
  const points: MonthlyProjectionPoint[] = [];

  for (let m = 1; m <= months; m++) {
    balance *= 1 + monthlyRate;
    const feeCharge = balance * totalFeeRate;
    balance -= feeCharge;
    fees += feeCharge;

    if (scenario.monthlyContribution > 0) {
      balance += scenario.monthlyContribution;
      totalContributed += scenario.monthlyContribution;
    }

    points.push({
      month: m,
      year: Math.ceil(m / 12),
      balance,
      totalContributed,
      grossGain: balance - totalContributed,
    });
  }

  return { points, totalContributed, grossFinal: balance, fees };
}

function scenarioTips(
  result: ComparisonScenarioResult,
  settings: ComparisonGlobalSettings,
): string[] {
  const tips: string[] = [];

  if (settings.adjustForInflation && result.realNetGain <= 0) {
    tips.push('Does not beat inflation — real purchasing power shrinks over this period.');
  }

  if (settings.deductTaxes && isTaxExempt(result.productType)) {
    tips.push('Tax-exempt for individuals (PF) — strong choice when rates are competitive.');
  }

  if (settings.deductTaxes && usesRegressiveIr(result.productType) && settings.years < 2) {
    tips.push('Short term raises IR (up to 22.5%). Longer holding reduces tax to 15%.');
  }

  if (settings.deductFees && result.fees > result.grossGain * 0.15 && result.fees > 0) {
    tips.push('Fees consume a large share of gains — compare lower-cost alternatives.');
  }

  if (result.productType === ComparisonProductType.Crypto) {
    tips.push('Crypto is volatile; projected return is illustrative, not guaranteed.');
  }

  if (result.productType === ComparisonProductType.Fii) {
    tips.push('FIIs often pay tax-free dividends; capital gains taxed at 20% on sale.');
  }

  return tips;
}

export function runComparison(
  scenarios: ComparisonScenarioInput[],
  settings: ComparisonGlobalSettings,
): ComparisonRunResult {
  const months = Math.max(1, Math.round(settings.years * 12));
  const results: ComparisonScenarioResult[] = scenarios.map((scenario) => {
    const effectiveAnnualRatePercent = resolveEffectiveAnnualRate(scenario, settings);
    const { points, totalContributed, grossFinal, fees } = projectMonthly(scenario, settings, months);
    const grossGain = grossFinal - totalContributed;
    const incomeTax = calculateIncomeTax(scenario.productType, grossGain, months, settings.deductTaxes);
    const netFinal = grossFinal - incomeTax;
    const netGain = netFinal - totalContributed;

    const inflationFactor = settings.adjustForInflation && settings.inflationPercent > 0
      ? Math.pow(1 + settings.inflationPercent / 100, settings.years)
      : 1;
    const realNetFinal = netFinal / inflationFactor;
    const realNetGain = realNetFinal - totalContributed;

    const netReturnPercent = totalContributed > 0 ? (netGain / totalContributed) * 100 : 0;
    const realReturnPercent = totalContributed > 0 ? (realNetGain / totalContributed) * 100 : 0;

    const result: ComparisonScenarioResult = {
      scenarioId: scenario.id,
      name: scenario.name,
      productType: scenario.productType,
      productLabel: ComparisonProductLabels[scenario.productType],
      effectiveAnnualRatePercent,
      totalContributed,
      grossFinal,
      grossGain,
      fees,
      incomeTax,
      netFinal,
      netGain,
      realNetFinal,
      realNetGain,
      realReturnPercent,
      netReturnPercent,
      monthlyPoints: points,
      tips: [],
    };

    result.tips = scenarioTips(result, settings);
    return result;
  });

  const rankedByNetGain = [...results].sort((a, b) => b.netGain - a.netGain);
  const rankedByRealGain = [...results].sort((a, b) => b.realNetGain - a.realNetGain);

  return {
    scenarios: results,
    rankedByNetGain,
    rankedByRealGain,
    globalTips: buildGlobalTips(results, settings, rankedByNetGain, rankedByRealGain, scenarios),
    settings,
    months,
  };
}

function buildGlobalTips(
  results: ComparisonScenarioResult[],
  settings: ComparisonGlobalSettings,
  rankedByNet: ComparisonScenarioResult[],
  rankedByReal: ComparisonScenarioResult[],
  scenarios: ComparisonScenarioInput[],
): string[] {
  if (results.length === 0)
    return ['Add at least one investment scenario to compare.'];

  const tips: string[] = [];
  const bestNet = rankedByNet[0];
  const bestReal = rankedByReal[0];

  tips.push(
    `Best net gain: ${bestNet.name} (${formatPct(bestNet.netReturnPercent)} return over ${settings.years} yr).`,
  );

  if (settings.adjustForInflation && bestReal.scenarioId !== bestNet.scenarioId) {
    tips.push(
      `Best inflation-adjusted: ${bestReal.name} — prioritize real returns when IPCA is high.`,
    );
  }

  const cdb = results.find((r) => r.productType === ComparisonProductType.Cdb);
  const lci = results.find((r) => r.productType === ComparisonProductType.Lci);
  const lca = results.find((r) => r.productType === ComparisonProductType.Lca);
  const exempt = [lci, lca].filter(Boolean) as ComparisonScenarioResult[];
  if (cdb && exempt.length > 0) {
    const bestExempt = exempt.sort((a, b) => b.netGain - a.netGain)[0];
    if (bestExempt.netGain > cdb.netGain && Math.abs(bestExempt.effectiveAnnualRatePercent - cdb.effectiveAnnualRatePercent) < 2) {
      tips.push('LCI/LCA beat CDB at similar rates because they are IR-exempt for individuals.');
    }
  }

  const savings = results.find((r) => r.productType === ComparisonProductType.Savings);
  if (savings && savings.realNetGain < 0 && settings.adjustForInflation) {
    tips.push('Poupança may not keep pace with IPCA — consider CDI-linked or inflation-indexed options.');
  }

  const variable = results.filter((r) =>
    r.productType === ComparisonProductType.Stock
    || r.productType === ComparisonProductType.Crypto
    || r.productType === ComparisonProductType.Fii,
  );
  if (variable.length > 0 && settings.years >= 10) {
    tips.push('Long horizons favor diversified portfolios; variable-income assumptions are user-defined.');
  }

  if (hasContributions(scenarios)) {
    tips.push('Monthly contributions significantly boost final patrimony — keep investing consistently.');
  }

  return tips;
}

function formatPct(value: number): string {
  return `${value.toFixed(1)}%`;
}

function hasContributions(scenarios: ComparisonScenarioInput[]): boolean {
  return scenarios.some((s) => s.monthlyContribution > 0);
}
