import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { FinancialInstitution } from '../../../models/financial-institution';
import { AccountType, AccountTypeLabels, Currency, CurrencyLabels } from '../../../models/investment';
import { InvestmentService } from '../../../shared/services/investment.service';
import { FinancialInstitutionService } from '../../../shared/services/financial-institution.service';

@Component({
  selector: 'app-investment-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule,
    MatCardModule, MatSnackBarModule, MatProgressSpinnerModule,
    MatSelectModule, MatDatepickerModule, MatNativeDateModule,
    MatSlideToggleModule,
  ],
  templateUrl: './investment-form.component.html',
  styleUrl: './investment-form.component.scss',
})
export class InvestmentFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(InvestmentService);
  private readonly institutionService = inject(FinancialInstitutionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  form!: FormGroup;
  isEdit = signal(false);
  loading = signal(false);
  saving = signal(false);
  entityId = signal<string | null>(null);
  institutions = signal<FinancialInstitution[]>([]);
  isFixedTerm = signal(false);
  showMonthlyMovement = signal(false);
  isForeignCurrency = signal(false);

  accountTypes = [
    { value: AccountType.CheckingAccount, label: AccountTypeLabels[AccountType.CheckingAccount] },
    { value: AccountType.SavingsBox, label: AccountTypeLabels[AccountType.SavingsBox] },
    { value: AccountType.FixedTerm, label: AccountTypeLabels[AccountType.FixedTerm] },
  ];

  currencies = [
    { value: Currency.BRL, label: CurrencyLabels[Currency.BRL] },
    { value: Currency.USD, label: CurrencyLabels[Currency.USD] },
    { value: Currency.EUR, label: CurrencyLabels[Currency.EUR] },
  ];

  ngOnInit(): void {
    this.form = this.fb.group({
      financialInstitutionId: ['', Validators.required],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      accountType: [AccountType.CheckingAccount, Validators.required],
      currency: [Currency.BRL, Validators.required],
      amount: [0, [Validators.required, Validators.min(0)]],
      cdiPercentage: [100, [Validators.required, Validators.min(0)]],
      annualRatePercent: [null as number | null],
      maturityDate: [null],
      requiresMonthlyMovement: [false],
      monthlyMovementAmount: [null],
    });

    this.form.get('accountType')!.valueChanges.subscribe((type: AccountType) => {
      this.isFixedTerm.set(type === AccountType.FixedTerm);
      if (type === AccountType.FixedTerm) {
        this.form.get('requiresMonthlyMovement')!.setValue(false);
        this.showMonthlyMovement.set(false);
      }
    });

    this.form.get('currency')!.valueChanges.subscribe((c: Currency) => {
      this.isForeignCurrency.set(c === Currency.USD || c === Currency.EUR);
      if (c === Currency.BRL) {
        this.form.get('cdiPercentage')!.setValidators([Validators.required, Validators.min(0)]);
        this.form.get('annualRatePercent')!.setValue(null);
      } else {
        this.form.get('cdiPercentage')!.clearValidators();
        this.form.get('cdiPercentage')!.setValue(0);
        this.form.get('annualRatePercent')!.setValidators([Validators.required, Validators.min(0)]);
      }
      this.form.get('cdiPercentage')!.updateValueAndValidity();
      this.form.get('annualRatePercent')!.updateValueAndValidity();
    });

    this.form.get('requiresMonthlyMovement')!.valueChanges.subscribe((val: boolean) => {
      this.showMonthlyMovement.set(val);
      if (!val) this.form.get('monthlyMovementAmount')!.setValue(null);
    });

    this.loadInstitutions();

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.entityId.set(id);
      this.loadEntity(id);
    }
  }

  async loadInstitutions(): Promise<void> {
    this.institutions.set(await this.institutionService.getAll());
  }

  async loadEntity(id: string): Promise<void> {
    this.loading.set(true);
    try {
      const entity = await this.service.getById(id);
      this.form.patchValue({
        financialInstitutionId: entity.financialInstitutionId,
        name: entity.name,
        accountType: entity.accountType,
        currency: entity.currency ?? Currency.BRL,
        amount: entity.amount,
        cdiPercentage: entity.cdiPercentage,
        annualRatePercent: entity.annualRatePercent ?? null,
        maturityDate: entity.maturityDate ? new Date(entity.maturityDate) : null,
        requiresMonthlyMovement: entity.requiresMonthlyMovement,
        monthlyMovementAmount: entity.monthlyMovementAmount,
      });
      this.isFixedTerm.set(entity.accountType === AccountType.FixedTerm);
      this.showMonthlyMovement.set(entity.requiresMonthlyMovement);
      this.isForeignCurrency.set(entity.currency === Currency.USD || entity.currency === Currency.EUR);
    } catch {
      this.snackBar.open('Failed to load investment.', 'Close', { duration: 3000 });
      this.router.navigate(['/investments']);
    } finally {
      this.loading.set(false);
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const body = { ...this.form.value };
      if (body.maturityDate instanceof Date) {
        body.maturityDate = body.maturityDate.toISOString();
      }

      if (this.isEdit() && this.entityId()) {
        await this.service.update(this.entityId()!, body);
        this.snackBar.open('Investment updated.', 'Close', { duration: 3000 });
      } else {
        await this.service.create(body);
        this.snackBar.open('Investment created.', 'Close', { duration: 3000 });
      }
      this.router.navigate(['/investments']);
    } catch {
      this.snackBar.open('Failed to save investment.', 'Close', { duration: 3000 });
    } finally {
      this.saving.set(false);
    }
  }

  onCancel(): void {
    this.router.navigate(['/investments']);
  }
}
