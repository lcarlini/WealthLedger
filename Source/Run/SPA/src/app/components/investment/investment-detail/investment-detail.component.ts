import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatChipsModule } from '@angular/material/chips';

import {
  AccountType,
  CurrencyLabels,
  Investment,
  InvestmentPerformance,
  InvestmentStatus,
  PerformanceBreakdownItem,
  PerformanceProjectionPoint,
} from '../../../models/investment';
import { InvestmentService } from '../../../shared/services/investment.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { TranslatePipe } from '../../../shared/i18n/translate.pipe';
import { LanguageService } from '../../../shared/i18n/language.service';
import { TranslationKey } from '../../../shared/i18n/translations';

@Component({
  selector: 'app-investment-detail',
  standalone: true,
  imports: [
    RouterLink, FormsModule, CurrencyPipe, DatePipe, DecimalPipe, NgClass,
    MatButtonModule, MatIconModule, MatCardModule, MatSnackBarModule,
    MatProgressSpinnerModule, MatDialogModule, MatTooltipModule,
    MatFormFieldModule, MatInputModule, MatChipsModule,
    TranslatePipe,
  ],
  templateUrl: './investment-detail.component.html',
  styleUrl: './investment-detail.component.scss',
})
export class InvestmentDetailComponent implements OnInit {
  private readonly service = inject(InvestmentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly lang = inject(LanguageService);

  loading = signal(true);
  investment = signal<Investment | null>(null);
  performance = signal<InvestmentPerformance | null>(null);
  redeemAmount = signal<number | null>(null);

  currencyCode = computed(() => {
    const inv = this.investment();
    return inv ? (CurrencyLabels[inv.currency] ?? 'BRL') : 'BRL';
  });

  verdictClass = computed(() => {
    const v = this.performance()?.verdict ?? '';
    if (v === 'beat_cdi') return 'verdict-good';
    if (v === 'lag_ipca') return 'verdict-bad';
    if (v === 'lag_cdi') return 'verdict-warn';
    return 'verdict-neutral';
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/investments']);
      return;
    }
    void this.load(id);
  }

  async load(id: string): Promise<void> {
    this.loading.set(true);
    try {
      const [inv, perf] = await Promise.all([
        this.service.getById(id),
        this.service.getPerformance(id),
      ]);
      this.investment.set(inv);
      this.performance.set(perf);
      this.redeemAmount.set(inv.amount);
    } catch {
      this.snackBar.open('Não foi possível carregar o investimento.', 'Fechar', { duration: 4000 });
      this.router.navigate(['/investments']);
    } finally {
      this.loading.set(false);
    }
  }

  navigateEdit(): void {
    const id = this.investment()?.id;
    if (id) this.router.navigate(['/investments', id, 'edit']);
  }

  async archive(): Promise<void> {
    const inv = this.investment();
    if (!inv) return;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Arquivar investimento',
        message: `Arquivar "${inv.name}"? O histórico fica disponível. Informe o valor resgatado se for diferente do atual.`,
      },
    });
    const confirmed = await firstValueFrom(dialogRef.afterClosed());
    if (!confirmed) return;

    try {
      await this.service.archive(inv.id, {
        redeemedAmount: this.redeemAmount() ?? inv.amount,
        notes: 'Resgate / encerramento',
      });
      this.snackBar.open('Investimento arquivado.', 'Fechar', { duration: 3000 });
      await this.load(inv.id);
    } catch {
      this.snackBar.open('Falha ao arquivar.', 'Fechar', { duration: 3000 });
    }
  }

  async restore(): Promise<void> {
    const inv = this.investment();
    if (!inv) return;
    try {
      await this.service.restore(inv.id);
      this.snackBar.open('Investimento reativado.', 'Fechar', { duration: 3000 });
      await this.load(inv.id);
    } catch {
      this.snackBar.open('Falha ao reativar.', 'Fechar', { duration: 3000 });
    }
  }

  async permanentDelete(): Promise<void> {
    const inv = this.investment();
    if (!inv) return;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Excluir permanentemente',
        message: `Apagar "${inv.name}" de forma irreversível? Prefira arquivar se quiser histórico.`,
      },
    });
    const confirmed = await firstValueFrom(dialogRef.afterClosed());
    if (!confirmed) return;
    try {
      await this.service.permanentDelete(inv.id);
      this.snackBar.open('Excluído permanentemente.', 'Fechar', { duration: 3000 });
      this.router.navigate(['/investments']);
    } catch {
      this.snackBar.open('Falha ao excluir.', 'Fechar', { duration: 3000 });
    }
  }

  isArchived(): boolean {
    return this.investment()?.status === InvestmentStatus.Archived;
  }

  needsHistorySetup(): boolean {
    const p = this.performance();
    if (!p || this.isArchived()) return false;
    return Math.abs(p.principalAmount - p.currentAmount) < 0.01 && Math.abs(p.grossGain) < 0.01;
  }

  accountLabel(type: AccountType): string {
    this.lang.lang();
    return this.lang.t(`account.${AccountType[type]}` as TranslationKey, AccountType[type]);
  }

  localizedVerdict(code: string): string {
    this.lang.lang();
    const key = `inv.verdict.${code}` as TranslationKey;
    const translated = this.lang.t(key);
    if (translated !== key) return translated;
    return this.performance()?.verdictSummary || code;
  }

  localizedBreakdown(p: InvestmentPerformance): PerformanceBreakdownItem[] {
    this.lang.lang();
    return (p.breakdown ?? []).map(item => {
      switch (item.key) {
        case 'principal':
          return { ...item, label: this.lang.t('inv.bd.principal') };
        case 'theoretical':
          return {
            ...item,
            label: this.lang.t('inv.bd.theoretical'),
            note: this.lang.t('inv.bd.theoreticalNote'),
          };
        case 'gross':
          return { ...item, label: this.lang.t('inv.bd.gross') };
        case 'iof':
          return {
            ...item,
            label: this.lang.t('inv.bd.iof').replace('{rate}', String(p.iofRatePercent ?? 0)),
            note: p.holdingDays < 30 ? this.lang.t('inv.bd.iofNoteEarly') : this.lang.t('inv.bd.iofNoteOk'),
          };
        case 'ir':
          return {
            ...item,
            label: p.isIncomeTaxExempt
              ? this.lang.t('inv.bd.irExempt')
              : this.lang.t('inv.bd.ir').replace('{rate}', String(p.irRatePercent ?? 0)),
            note: p.isIncomeTaxExempt ? this.lang.t('inv.bd.irNoteExempt') : this.lang.t('inv.bd.irNote'),
          };
        case 'net':
          return { ...item, label: this.lang.t('inv.bd.net') };
        default:
          return item;
      }
    });
  }

  localizedProjections(p: InvestmentPerformance): PerformanceProjectionPoint[] {
    this.lang.lang();
    const map: Record<number, TranslationKey> = {
      3: 'inv.proj.3m',
      6: 'inv.proj.6m',
      12: 'inv.proj.1y',
      24: 'inv.proj.2y',
      36: 'inv.proj.3y',
    };
    return (p.projections ?? []).map(pt => ({
      ...pt,
      label: map[pt.monthsAhead] ? this.lang.t(map[pt.monthsAhead]) : pt.label,
    }));
  }
}
