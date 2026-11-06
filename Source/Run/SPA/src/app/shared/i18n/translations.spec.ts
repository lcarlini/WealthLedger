import { TRANSLATIONS } from './translations';

describe('translations', () => {
  it('define the same keys in English and Portuguese', () => {
    const en = new Set(Object.keys(TRANSLATIONS.en));
    const pt = new Set(Object.keys(TRANSLATIONS.pt));

    const missingInPt = [...en].filter(key => !pt.has(key));
    const missingInEn = [...pt].filter(key => !en.has(key));

    expect(missingInPt).toEqual([]);
    expect(missingInEn).toEqual([]);
  });

  it('have no empty values', () => {
    for (const lang of ['en', 'pt'] as const) {
      const empty = Object.entries(TRANSLATIONS[lang])
        .filter(([, value]) => typeof value !== 'string' || value.trim() === '')
        .map(([key]) => key);
      expect(empty).withContext(lang).toEqual([]);
    }
  });
});
