// Cálculos del selector de fecha y hora. Sin React, para poder probarlos. Todo en la hora local de quien lo usa.

export interface DayCell {
  year: number;
  /** 0 = enero … 11 = diciembre. */
  month: number;
  day: number;
  /** Falso en los días sobrantes de los meses vecinos. */
  inMonth: boolean;
}

export const WEEKDAYS = ["Lu", "Ma", "Mi", "Ju", "Vi", "Sá", "Do"] as const;

/** Siempre 6 semanas de 7 días (lunes primero), para que el calendario no cambie de alto de un mes a otro. */
export function monthGrid(year: number, month: number): DayCell[][] {
  const first = new Date(year, month, 1);
  const offset = (first.getDay() + 6) % 7; // lunes = 0
  const weeks: DayCell[][] = [];
  for (let w = 0; w < 6; w++) {
    const week: DayCell[] = [];
    for (let d = 0; d < 7; d++) {
      const date = new Date(year, month, 1 - offset + w * 7 + d);
      week.push({ year: date.getFullYear(), month: date.getMonth(), day: date.getDate(), inMonth: date.getMonth() === month });
    }
    weeks.push(week);
  }
  return weeks;
}

const pad = (n: number) => String(n).padStart(2, "0");

/** «2026-10-21T22:23» → partes numéricas. null si no tiene ese formato. */
export function parseLocal(local: string): { year: number; month: number; day: number; hour: number; minute: number } | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(local);
  if (!m) return null;
  return { year: Number(m[1]), month: Number(m[2]) - 1, day: Number(m[3]), hour: Number(m[4]), minute: Number(m[5]) };
}

export function formatLocal(year: number, month: number, day: number, hour: number, minute: number): string {
  return `${year}-${pad(month + 1)}-${pad(day)}T${pad(hour)}:${pad(minute)}`;
}

/** «HH:mm» de una hora local. */
export function timeOf(local: string): string {
  return local.slice(11, 16);
}

/** Horas redondas cada `step` minutos: 00:00, 00:15… */
export function timeSlots(step = 15): string[] {
  const slots: string[] = [];
  for (let minutes = 0; minutes < 24 * 60; minutes += step) slots.push(`${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`);
  return slots;
}

/** Compara dos fechas locales (AAAA-MM-DDTHH:mm) como texto: el formato ya es ordenable. */
export const isAfter = (a: string, b: string) => a > b;

/** Mueve la fecha elegida a otro día manteniendo la hora, y la recorta si pasa del límite. */
export function pickDay(current: string, cell: Pick<DayCell, "year" | "month" | "day">, max: string): string {
  const time = timeOf(current) || "00:00";
  const next = `${cell.year}-${pad(cell.month + 1)}-${pad(cell.day)}T${time}`;
  return isAfter(next, max) ? max : next;
}
