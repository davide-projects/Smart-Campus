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

// Es. «giovedì 5 novembre 2026, 10:00–11:00».
export function reservationTimeLabel(item: Pick<MyReservationItem, 'start' | 'end'>): string {
  const start = new Date(item.start);
  const end = new Date(item.end);
  return `${DAY_FORMAT.format(start)}, ${TIME_FORMAT.format(start)}–${TIME_FORMAT.format(end)}`;
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
