import { campusTime, formatTimeRange } from './booking.model';

export interface AvailabilitySlot {
  start: string;
  end: string;
  isFree: boolean;
}
export interface DailyAvailability {
  roomId: string;
  date: string;
  isOpen: boolean;
  slots: AvailabilitySlot[];
}

// (GET /api/bookings/availability/search)
export interface AvailableRoomResponse {
  roomId: string;
  name: string;
  buildingName: string | null;
  type: string | null;
  capacity: number;
}

export interface AvailabilitySearchFilter {
  date: string;
  start: string;
  end: string;
  type?: string | null;
  minCapacity?: number | null;
}

// --- Linea del tempo della giornata (dettaglio spazio) ---

// Blocco della linea del tempo: fasce consecutive con lo stesso stato, unite in un solo tratto.
export interface TimelineBlock {
  start: string;
  end: string;
  isFree: boolean;
  minutes: number;
  // Orario in ora del campus, es. «08:00–11:30».
  range: string;
  // Testo per il suggerimento che compare con il mouse o con la tastiera, es. «Libera · 08:00–11:30».
  tooltip: string;
  // Testo per i lettori di schermo, es. «Libera dalle 08:00 alle 11:30».
  srText: string;
  // Il blocco è abbastanza largo da mostrare l'orario al suo interno (su schermi larghi / stretti).
  fitsWide: boolean;
  fitsNarrow: boolean;
}

// Riferimento orario sotto la barra. `percent` è la posizione lungo la barra.
export interface TimelineTick {
  label: string;
  percent: number;
  edge: 'first' | 'last' | null;
  // Gli orari interni si mostrano tutti sugli schermi larghi e uno ogni tre ore sugli stretti.
  showOnNarrow: boolean;
}

export interface DayTimeline {
  blocks: TimelineBlock[];
  ticks: TimelineTick[];
  freeMinutes: number;
  totalMinutes: number;
  // Es. «Libero 7 h 30 min» o «Nessuna fascia libera», e «su 12 h».
  freeLabel: string;
  totalLabel: string;
}

// Un blocco mostra l'orario dentro di sé solo se ha spazio: soglie in percentuale della giornata,
// ricavate dalla larghezza del testo «08:00–11:30» su una barra larga (circa 830 px) e stretta (circa 340 px).
const WIDE_LABEL_MIN_SHARE = 0.135;
const NARROW_LABEL_MIN_SHARE = 0.28;

// Un riferimento interno non si mostra se è troppo vicino al primo o all'ultimo orario.
const WIDE_TICK_MIN_DISTANCE = 45;
const NARROW_TICK_MIN_DISTANCE = 90;

const minutesBetween = (fromIso: string, toIso: string): number =>
  Math.round((Date.parse(toIso) - Date.parse(fromIso)) / 60_000);

const timeOf = (iso: string): string => campusTime(new Date(iso));

// Es. 450 → «7 h 30 min», 720 → «12 h», 30 → «30 min».
export function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) {
    return `${rest} min`;
  }
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}

// Trasforma le fasce da 30 minuti restituite dall'API (contigue, dall'apertura alla chiusura)
// nella linea del tempo: blocchi uniti, riferimenti orari e riepilogo. Senza fasce restituisce null.
export function buildTimeline(slots: AvailabilitySlot[]): DayTimeline | null {
  if (slots.length === 0) {
    return null;
  }

  const merged: { start: string; end: string; isFree: boolean }[] = [];
  for (const slot of slots) {
    const last = merged[merged.length - 1];
    if (last && last.isFree === slot.isFree) {
      last.end = slot.end;
    } else {
      merged.push({ start: slot.start, end: slot.end, isFree: slot.isFree });
    }
  }

  const dayStart = merged[0].start;
  const dayEnd = merged[merged.length - 1].end;
  const totalMinutes = minutesBetween(dayStart, dayEnd);
  if (totalMinutes <= 0) {
    return null;
  }

  const blocks: TimelineBlock[] = merged.map((block) => {
    const minutes = minutesBetween(block.start, block.end);
    const share = minutes / totalMinutes;
    const state = block.isFree ? 'Libera' : 'Occupata';
    const range = formatTimeRange(block.start, block.end, '–');
    return {
      ...block,
      minutes,
      range,
      tooltip: `${state} · ${range}`,
      srText: `${state} dalle ${timeOf(block.start)} alle ${timeOf(block.end)}`,
      fitsWide: share >= WIDE_LABEL_MIN_SHARE,
      fitsNarrow: share >= NARROW_LABEL_MIN_SHARE,
    };
  });

  const freeMinutes = blocks.filter((block) => block.isFree).reduce((sum, b) => sum + b.minutes, 0);

  // Riferimenti: primo e ultimo orario sempre, poi le ore intere abbastanza lontane dai due estremi.
  const ticks: TimelineTick[] = [
    { label: timeOf(dayStart), percent: 0, edge: 'first', showOnNarrow: true },
  ];
  const boundaries = [...new Set(slots.map((slot) => slot.start))].filter((iso) => iso !== dayStart);
  let hourIndex = 0;
  for (const iso of boundaries) {
    const label = timeOf(iso);
    const elapsed = minutesBetween(dayStart, iso);
    const remaining = totalMinutes - elapsed;
    if (!label.endsWith(':00') || elapsed < WIDE_TICK_MIN_DISTANCE || remaining < WIDE_TICK_MIN_DISTANCE) {
      continue;
    }
    ticks.push({
      label,
      percent: (elapsed / totalMinutes) * 100,
      edge: null,
      // Sugli schermi stretti si mostra un'ora ogni tre (la terza, la sesta…), non troppo vicina agli estremi.
      showOnNarrow:
        hourIndex % 3 === 2 && elapsed >= NARROW_TICK_MIN_DISTANCE && remaining >= NARROW_TICK_MIN_DISTANCE,
    });
    hourIndex++;
  }
  ticks.push({ label: timeOf(dayEnd), percent: 100, edge: 'last', showOnNarrow: true });

  return {
    blocks,
    ticks,
    freeMinutes,
    totalMinutes,
    freeLabel: freeMinutes === 0 ? 'Nessuna fascia libera' : `Libero ${formatDuration(freeMinutes)}`,
    totalLabel: `su ${formatDuration(totalMinutes)}`,
  };
}
