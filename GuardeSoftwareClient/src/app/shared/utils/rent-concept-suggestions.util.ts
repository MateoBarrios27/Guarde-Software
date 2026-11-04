const MONTHS = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'
];

/** `month` va de 0 (enero) a 11 (diciembre), como `Date#getMonth`. */
export interface RentConceptMonth {
  year: number;
  month: number;
}

export interface RentConceptMovement {
  movementType: string;
  concept: string | null | undefined;
}

/**
 * Mes de un débito de alquiler a partir de su concepto: «Alquiler Noviembre 2026»,
 * «Alquiler noviembre 2026 (Planificado)», «Alquiler Octubre 2026 (Proporcional 19 días)»...
 * Devuelve null si el concepto no es de alquiler (intereses, ajustes, textos libres).
 */
export function parseRentConceptMonth(concept: string | null | undefined): RentConceptMonth | null {
  const match = /^\s*alquiler\s+([a-záéíóúñ]+)\s+(\d{4})\b/i.exec(concept ?? '');
  if (!match) return null;

  const name = match[1].toLowerCase() === 'setiembre' ? 'septiembre' : match[1].toLowerCase();
  const month = MONTHS.indexOf(name);
  return month < 0 ? null : { year: Number(match[2]), month };
}

/** Último mes de alquiler que el cliente ya tiene debitado, o null si no tiene ninguno. */
export function getLatestRentDebitMonth(movements: readonly RentConceptMovement[]): RentConceptMonth | null {
  let latest: RentConceptMonth | null = null;
  for (const movement of movements) {
    if (movement.movementType !== 'DEBITO') continue;
    const parsed = parseRentConceptMonth(movement.concept);
    if (!parsed) continue;
    if (!latest || parsed.year * 12 + parsed.month > latest.year * 12 + latest.month) latest = parsed;
  }
  return latest;
}

/**
 * Los dos conceptos de alquiler a sugerir: los dos meses siguientes al último débito
 * de alquiler del cliente (con octubre y noviembre ya generados, diciembre y enero).
 * Sin débitos de alquiler, el mes en curso y el siguiente.
 */
export function suggestRentConcepts(
  movements: readonly RentConceptMovement[],
  today: Date = new Date()
): string[] {
  const latest = getLatestRentDebitMonth(movements);
  const first = latest
    ? new Date(latest.year, latest.month + 1, 1)
    : new Date(today.getFullYear(), today.getMonth(), 1);

  return [0, 1].map(offset => {
    const date = new Date(first.getFullYear(), first.getMonth() + offset, 1);
    const name = MONTHS[date.getMonth()];
    return `Alquiler ${name.charAt(0).toUpperCase()}${name.slice(1)} ${date.getFullYear()}`;
  });
}
