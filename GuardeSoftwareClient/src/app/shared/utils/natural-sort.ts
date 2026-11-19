// Keep the previous locale and options; reuse ICU comparators across all rows.
export const naturalIdentifierCollator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
export const textCollator = new Intl.Collator();
export const spanishTextCollator = new Intl.Collator('es');
