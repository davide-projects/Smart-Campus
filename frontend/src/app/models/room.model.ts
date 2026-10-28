// Tipo di spazio come lo invia il servizio Rooms (es. "StudyRoom"). Non è un elenco chiuso:
// i tipi disponibili nel filtro si ricavano dagli spazi che esistono.
export type RoomType = string;

// Etichette italiane dei tipi noti.
const ROOM_TYPE_LABELS: Record<RoomType, string> = {
  Classroom: 'Aula',
  Laboratory: 'Laboratorio',
  StudyRoom: 'Sala studio',
};

// Etichetta da mostrare all'utente: un tipo nuovo ancora senza traduzione compare con il valore del backend.
export function roomTypeLabel(type: RoomType): string {
  return ROOM_TYPE_LABELS[type] ?? type;
}

// Elemento della risposta di GET /api/rooms (ricerca).
export interface RoomSummary {
  id: string;
  name: string;
  type: RoomType;
  capacity: number;
  floor: number;
  isBookable: boolean;
  buildingId: string;
  buildingName: string;
}

// Filtri della ricerca: tutti opzionali.
export interface RoomFilter {
  type?: RoomType | null;
  minCapacity?: number | null;
  buildingId?: string | null;
}

// Attrezzatura di uno spazio nel dettaglio.
export interface RoomEquipment {
  id: string;
  name: string;
}

// Giorni della settimana come inviati dal servizio Rooms (serializzati come stringhe).
export type DayOfWeek =
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'
  | 'Sunday';

// Etichette italiane dei giorni della settimana.
const DAY_OF_WEEK_LABELS: Record<string, string> = {
  Monday: 'Lunedì',
  Tuesday: 'Martedì',
  Wednesday: 'Mercoledì',
  Thursday: 'Giovedì',
  Friday: 'Venerdì',
  Saturday: 'Sabato',
  Sunday: 'Domenica',
};

export function dayOfWeekLabel(day: string): string {
  return DAY_OF_WEEK_LABELS[day] ?? day;
}

// Orario di apertura settimanale di uno spazio.
export interface OpeningHours {
  dayOfWeek: DayOfWeek;
  opensAt: string | null;
  closesAt: string | null;
  isClosed: boolean;
}

// Risposta completa di GET /api/rooms/{id} (dettaglio).
export interface RoomDetail {
  id: string;
  name: string;
  type: RoomType;
  buildingId: string;
  buildingName: string;
  buildingCode: string;
  floor: number;
  capacity: number;
  isBookable: boolean;
  equipment: RoomEquipment[];
  openingHours: OpeningHours[];
}
