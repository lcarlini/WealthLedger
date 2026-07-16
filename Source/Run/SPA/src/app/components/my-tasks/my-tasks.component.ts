import { Component, inject, signal, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatBadgeModule } from '@angular/material/badge';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TaskItem, FutureTask } from '../../models/task-item';
import { TaskService } from '../../shared/services/task.service';

@Component({
  selector: 'app-my-tasks',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    FormsModule,
    MatTableModule, MatButtonModule, MatIconModule, MatCardModule,
    MatSnackBarModule, MatProgressSpinnerModule, MatChipsModule,
    MatTooltipModule, MatTabsModule, MatBadgeModule, MatFormFieldModule, MatInputModule,
  ],
  templateUrl: './my-tasks.component.html',
  styleUrl: './my-tasks.component.scss',
})
export class MyTasksComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly snackBar = inject(MatSnackBar);

  pendingColumns = ['status', 'title', 'institutionName', 'dueMonth', 'requiredAmount', 'actions'];
  completedColumns = ['title', 'institutionName', 'dueMonth', 'requiredAmount', 'completedDate'];
  futureColumns = ['icon', 'title', 'institutionName', 'dueDate', 'amount', 'taskType'];

  tasks = signal<TaskItem[]>([]);
  completedTasks = signal<TaskItem[]>([]);
  futureTasks = signal<FutureTask[]>([]);
  loading = signal(false);
  loadingCompleted = signal(false);
  loadingFuture = signal(false);
  futureProjectionYears = signal(3);

  private readonly monthNames = [
    'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun',
    'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'
  ];

  ngOnInit(): void {
    this.loadTasks();
    this.loadCompleted();
    this.loadFuture();
  }

  async loadTasks(): Promise<void> {
    this.loading.set(true);
    try {
      this.tasks.set(await this.taskService.getPending());
    } catch {
      this.snackBar.open('Failed to load tasks.', 'Close', { duration: 3000 });
    } finally {
      this.loading.set(false);
    }
  }

  async loadCompleted(): Promise<void> {
    this.loadingCompleted.set(true);
    try {
      this.completedTasks.set(await this.taskService.getCompleted());
    } catch {
      this.snackBar.open('Failed to load history.', 'Close', { duration: 3000 });
    } finally {
      this.loadingCompleted.set(false);
    }
  }

  async loadFuture(): Promise<void> {
    this.loadingFuture.set(true);
    try {
      const years = Math.max(1, Math.floor(this.futureProjectionYears() || 1));
      this.futureProjectionYears.set(years);
      this.futureTasks.set(await this.taskService.getFuture(years * 12));
    } catch {
      this.snackBar.open('Failed to load future tasks.', 'Close', { duration: 3000 });
    } finally {
      this.loadingFuture.set(false);
    }
  }

  formatDueMonth(task: TaskItem): string {
    const month = this.monthNames[task.dueMonth - 1];
    if (!this.isMonthlyMovement(task) && task.dueDay) {
      return `${task.dueDay} ${month} ${task.dueYear}`;
    }
    return `${month} ${task.dueYear}`;
  }

  formatFutureDue(task: FutureTask): string {
    const month = this.monthNames[task.dueMonth - 1];
    if (task.taskType !== 'MonthlyMovement' && task.dueDay) {
      return `${task.dueDay} ${month} ${task.dueYear}`;
    }
    return `${month} ${task.dueYear}`;
  }

  isMonthlyMovement(task: TaskItem | FutureTask): boolean {
    if ('taskType' in task && task.taskType === 'MonthlyMovement') return true;
    return task.title.toLowerCase().startsWith('monthly movement:');
  }

  private dueDate(task: TaskItem | FutureTask): Date {
    if (this.isMonthlyMovement(task)) {
      const lastDay = new Date(task.dueYear, task.dueMonth, 0).getDate();
      return new Date(task.dueYear, task.dueMonth - 1, lastDay);
    }
    const day = task.dueDay ?? 1;
    return new Date(task.dueYear, task.dueMonth - 1, day);
  }

  private isInDueMonth(task: TaskItem): boolean {
    const today = new Date();
    return today.getFullYear() === task.dueYear && today.getMonth() + 1 === task.dueMonth;
  }

  private isPastDueMonth(task: TaskItem): boolean {
    const today = new Date();
    if (today.getFullYear() > task.dueYear) return true;
    return today.getFullYear() === task.dueYear && today.getMonth() + 1 > task.dueMonth;
  }

  isOverdue(task: TaskItem): boolean {
    if (this.isMonthlyMovement(task)) {
      return this.isPastDueMonth(task);
    }
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return this.dueDate(task) < today;
  }

  isDueSoon(task: TaskItem): boolean {
    if (this.isMonthlyMovement(task)) {
      return this.isInDueMonth(task) && !this.isPastDueMonth(task);
    }
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const due = this.dueDate(task);
    const diffDays = Math.ceil((due.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
    return diffDays >= 0 && diffDays <= 7;
  }

  isActionable(task: TaskItem): boolean {
    if (this.isMonthlyMovement(task)) {
      return this.isInDueMonth(task) || this.isPastDueMonth(task);
    }
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return this.dueDate(task) <= today;
  }

  getTaskTypeLabel(type: string): string {
    switch (type) {
      case 'Maturity': return 'Maturity';
      case 'MonthlyMovement': return 'Monthly Movement';
      default: return type;
    }
  }

  getTaskTypeIcon(type: string): string {
    switch (type) {
      case 'Maturity': return 'event';
      case 'MonthlyMovement': return 'repeat';
      default: return 'schedule';
    }
  }

  formatNumber(value: number): string {
    return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  async markAsDone(task: TaskItem): Promise<void> {
    try {
      await this.taskService.complete(task.id);
      this.snackBar.open('Task marked as completed!', 'Close', { duration: 3000 });
      this.loadTasks();
      this.loadCompleted();
    } catch {
      this.snackBar.open('Failed to complete task.', 'Close', { duration: 3000 });
    }
  }
}
