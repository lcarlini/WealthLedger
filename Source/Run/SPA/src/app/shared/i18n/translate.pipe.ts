import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from './language.service';
import { TranslationKey } from './translations';

@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly lang = inject(LanguageService);

  transform(key: TranslationKey | string, fallback?: string): string {
    // Touch signal so impure pipe refreshes on language change
    this.lang.lang();
    return this.lang.t(key, fallback);
  }
}
