import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full',
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./components/dashboard/dashboard.component').then(m => m.DashboardComponent),
  },
  {
    path: 'financial-institutions',
    loadComponent: () =>
      import('./components/financial-institution/financial-institution-list/financial-institution-list.component')
        .then(m => m.FinancialInstitutionListComponent),
  },
  {
    path: 'financial-institutions/new',
    loadComponent: () =>
      import('./components/financial-institution/financial-institution-form/financial-institution-form.component')
        .then(m => m.FinancialInstitutionFormComponent),
  },
  {
    path: 'financial-institutions/:id/edit',
    loadComponent: () =>
      import('./components/financial-institution/financial-institution-form/financial-institution-form.component')
        .then(m => m.FinancialInstitutionFormComponent),
  },
  {
    path: 'investments',
    loadComponent: () =>
      import('./components/investment/investment-list/investment-list.component')
        .then(m => m.InvestmentListComponent),
  },
  {
    path: 'investments/new',
    loadComponent: () =>
      import('./components/investment/investment-form/investment-form.component')
        .then(m => m.InvestmentFormComponent),
  },
  {
    path: 'investments/:id/edit',
    loadComponent: () =>
      import('./components/investment/investment-form/investment-form.component')
        .then(m => m.InvestmentFormComponent),
  },
  {
    path: 'my-tasks',
    loadComponent: () =>
      import('./components/my-tasks/my-tasks.component').then(m => m.MyTasksComponent),
  },
  {
    path: 'control',
    loadComponent: () =>
      import('./components/control/control.component').then(m => m.ControlComponent),
  },
  {
    path: 'import',
    loadComponent: () =>
      import('./components/import/import.component').then(m => m.ImportComponent),
  },
  {
    path: 'analytics',
    loadComponent: () =>
      import('./components/analytics/analytics.component').then(m => m.AnalyticsComponent),
  },
  {
    path: 'calculator',
    loadComponent: () =>
      import('./components/calculator/calculator.component').then(m => m.CalculatorComponent),
  },
];
