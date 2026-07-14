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

import { FinancialInstitutionService } from '../../../shared/services/financial-institution.service';

@Component({
  selector: 'app-financial-institution-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatCardModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './financial-institution-form.component.html',
  styleUrl: './financial-institution-form.component.scss',
})
export class FinancialInstitutionFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(FinancialInstitutionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  form!: FormGroup;
  isEdit = signal(false);
  loading = signal(false);
  saving = signal(false);
  entityId = signal<string | null>(null);
  imagePreview = signal<string | null>(null);

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.maxLength(1000)]],
      imageUrl: [''],
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.entityId.set(id);
      this.loadEntity(id);
    }
  }

  async loadEntity(id: string): Promise<void> {
    this.loading.set(true);
    try {
      const entity = await this.service.getById(id);
      this.form.patchValue({
        name: entity.name,
        description: entity.description,
        imageUrl: entity.imageUrl,
      });
      if (entity.imageUrl) {
        this.imagePreview.set(entity.imageUrl);
      }
    } catch {
      this.snackBar.open('Failed to load financial institution.', 'Close', { duration: 3000 });
      this.router.navigate(['/financial-institutions']);
    } finally {
      this.loading.set(false);
    }
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;

    const file = input.files[0];
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      this.imagePreview.set(result);
      this.form.patchValue({ imageUrl: result });
    };
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    this.imagePreview.set(null);
    this.form.patchValue({ imageUrl: '' });
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const body = this.form.value;

      if (this.isEdit() && this.entityId()) {
        await this.service.update(this.entityId()!, body);
        this.snackBar.open('Financial institution updated.', 'Close', { duration: 3000 });
      } else {
        await this.service.create(body);
        this.snackBar.open('Financial institution created.', 'Close', { duration: 3000 });
      }

      this.router.navigate(['/financial-institutions']);
    } catch {
      this.snackBar.open('Failed to save financial institution.', 'Close', { duration: 3000 });
    } finally {
      this.saving.set(false);
    }
  }

  onCancel(): void {
    this.router.navigate(['/financial-institutions']);
  }
}
