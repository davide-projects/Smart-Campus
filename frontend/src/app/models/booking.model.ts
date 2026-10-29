// Una prenotazione dell'utente, come organizzatore o come membro (GET /api/bookings/mine).
// buildingName e organizerName sono null quando Bookings non li conosce (dati precedenti a US-07).
export interface MyReservationItem {
  id: string;
  roomId: string;
  roomName: string;
  buildingName: string | null;
  start: string;
  end: string;
  isGroup: boolean;
  organizerName: string | null;
  isOrganizer: boolean;
}

// Risposta di GET /api/bookings/mine: le tre sezioni della pagina «Le mie prenotazioni».
export interface MyReservationsResponse {
  upcoming: MyReservationItem[];
  past: MyReservationItem[];
  cancelled: MyReservationItem[];
}

// Richiesta di POST /api/bookings: prenotazione singola a nome dell'utente del token.
// start ed end sono date-ora ISO 8601 con lo scostamento del campus (es. 2026-11-05T10:00:00+01:00).
export interface CreateReservationRequest {
  roomId: string;
  start: string;
  end: string;
  purpose: string | null;
}

// Stato di una prenotazione come lo invia Bookings (es. "Confirmed").
export type ReservationStatus = 'Confirmed' | 'Cancelled';

// Conferma restituita da POST /api/bookings con il 201.
export interface ReservationResponse {
  id: string;
  roomId: string;
  roomName: string;
  start: string;
  end: string;
  participants: number;
  status: ReservationStatus;
}

// Risposta di GET /api/bookings/rooms/{id}/future-reservations-count (solo personale).
export interface FutureReservationsCountResponse {
  roomId: string;
  count: number;
}

// Elemento dell'elenco delle prenotazioni di uno spazio per un giorno (GET /api/bookings?roomId=...&date=...).
export interface ReservationListItem {
  id: string;
  start: string;
  end: string;
  organizerId: string;
  organizerName?: string | null;
  participants: number;
  memberIds: string[];
  status: ReservationStatus;
  cancelledAt: string | null;
}

// Giorno e orario si mostrano sempre nell'ora del campus, qualunque sia il fuso del dispositivo.
const CAMPUS_TIME_ZONE = 'Europe/Rome';

const DAY_FORMAT = new Intl.DateTimeFormat('it-IT', {
  timeZone: CAMPUS_TIME_ZONE,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

const TIME_FORMAT = new Intl.DateTimeFormat('it-IT', {
  timeZone: CAMPUS_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
});

// Orario della fascia in ora del campus, es. «10:00 – 11:00» o con separatore personalizzato.
export function formatTimeRange(startIso: string, endIso: string, separator = ' – '): string {
  const start = new Date(startIso);
  const end = new Date(endIso);
  return `${TIME_FORMAT.format(start)}${separator}${TIME_FORMAT.format(end)}`;
}

// Es. «giovedì 5 novembre 2026, 10:00–11:00».
export function reservationTimeLabel(item: { start: string; end: string }): string {
  const start = new Date(item.start);
  return `${DAY_FORMAT.format(start)}, ${formatTimeRange(item.start, item.end, '–')}`;
}

// Scostamento dall'ora UTC del campus in un istante, es. «GMT+01:00» (solo «GMT» quando è zero).
const OFFSET_FORMAT = new Intl.DateTimeFormat('en-US', {
  timeZone: CAMPUS_TIME_ZONE,
  timeZoneName: 'longOffset',
});

const DATE_PARTS_FORMAT = new Intl.DateTimeFormat('en-US', {
  timeZone: CAMPUS_TIME_ZONE,
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
});

// Giorno di oggi nell'ora del campus, come data locale a mezzanotte (il formato dei calendari).
export function campusToday(now: Date = new Date()): Date {
  const parts = DATE_PARTS_FORMAT.formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((p) => p.type === type)!.value);
  return new Date(part('year'), part('month') - 1, part('day'));
}

const CLOCK_FORMAT = new Intl.DateTimeFormat('en-US', {
  timeZone: CAMPUS_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

// Ora attuale del campus come «HH:mm», es. «09:10».
export function campusTime(now: Date = new Date()): string {
  const parts = CLOCK_FORMAT.formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)!.value;
  return `${part('hour')}:${part('minute')}`;
}

// Data-ora ISO 8601 con lo scostamento del campus per il giorno scelto (letto come data del
// calendario, senza fuso) e l'ora «HH:mm» del campus. Es. 5 novembre 2026, «10:00» →
// «2026-11-05T10:00:00+01:00»; a giugno lo scostamento è +02:00 per l'ora legale.
export function campusDateTime(day: Date, time: string): string {
  const [hours, minutes] = time.split(':').map(Number);
  const year = day.getFullYear();
  const month = day.getMonth() + 1;
  const date = day.getDate();

  // L'ora scritta letta come UTC è vicina all'istante cercato: si legge lo scostamento lì e lo si
  // ricontrolla nell'istante corretto, così anche i giorni del cambio d'ora restano esatti.
  const wallClock = Date.UTC(year, month - 1, date, hours, minutes);
  const firstGuess = campusOffsetMinutes(wallClock);
  const offset = campusOffsetMinutes(wallClock - firstGuess * 60_000);

  return (
    `${pad(year, 4)}-${pad(month)}-${pad(date)}T${pad(hours)}:${pad(minutes)}:00` +
    formatOffset(offset)
  );
}

function campusOffsetMinutes(instant: number): number {
  const name = OFFSET_FORMAT.formatToParts(new Date(instant)).find(
    (part) => part.type === 'timeZoneName',
  )!.value;
  const match = /^GMT([+-])(\d{2}):(\d{2})$/.exec(name);
  if (!match) {
    return 0;
  }
  const minutes = Number(match[2]) * 60 + Number(match[3]);
  return match[1] === '-' ? -minutes : minutes;
}

function formatOffset(offsetMinutes: number): string {
  const sign = offsetMinutes < 0 ? '-' : '+';
  const absolute = Math.abs(offsetMinutes);
  return `${sign}${pad(Math.floor(absolute / 60))}:${pad(absolute % 60)}`;
}

export function pad(value: number, length = 2): string {
  return String(value).padStart(length, '0');
}

// Organizzatore di una prenotazione di gruppo: «Tu» ha la precedenza sul nome mancante.
export function organizerLabel(
  item: Pick<MyReservationItem, 'isOrganizer' | 'organizerName'>,
): string {
  if (item.isOrganizer) {
    return 'Organizzatore: Tu';
  }
  return item.organizerName === null
    ? 'Organizzatore non disponibile'
    : `Organizzatore: ${item.organizerName}`;
}

// Converte un oggetto Date nel formato "YYYY-MM-DD" richiesto come parametro query dall'API
export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

// Aggiunge un numero di giorni a una data (es. per calcolare la finestra massima di 14 giorni)
export function addDays(day: Date, days: number): Date {
  return new Date(day.getFullYear(), day.getMonth(), day.getDate() + days);
}

// «08:00:00» o «08:30» → 480 / 510 minuti da mezzanotte.
export function toMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

// Orari «HH:mm» all'ora esatta o alla mezz'ora, da opensAt a closesAt compresi.
export function halfHourSlots(
  opensOrHours: string | { opensAt: string | null; closesAt: string | null } = '08:00',
  closesAtParam = '20:00',
): string[] {
  let opensAt = '08:00';
  let closesAt = closesAtParam;
  if (typeof opensOrHours === 'object' && opensOrHours !== null) {
    opensAt = opensOrHours.opensAt ?? '08:00';
    closesAt = opensOrHours.closesAt ?? '20:00';
  } else if (typeof opensOrHours === 'string') {
    opensAt = opensOrHours;
  }
  const opens = toMinutes(opensAt);
  const closes = toMinutes(closesAt);
  const slots: string[] = [];
  for (let minutes = Math.ceil(opens / 30) * 30; minutes <= closes; minutes += 30) {
    slots.push(`${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`);
  }
  return slots;
}

// Calcola la fascia oraria di ricerca predefinita per oggi in ora del campus (prossima mezz'ora utile).
export function defaultSearchSlot(now: Date = new Date()): { start: string; end: string } {
  const nowCampus = campusTime(now);
  const starts = halfHourSlots('08:00', '19:30').filter((slot) => slot > nowCampus);
  if (starts.length === 0) {
    return { start: '09:00', end: '10:00' };
  }
  const start = starts[0];
  const endMinutes = Math.min(toMinutes(start) + 60, 20 * 60);
  const end = `${pad(Math.floor(endMinutes / 60))}:${pad(endMinutes % 60)}`;
  return { start, end };
}
