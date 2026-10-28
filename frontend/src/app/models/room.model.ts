// Tipo di spazio come lo invia il servizio Rooms (es. "StudyRoom"). Non è un elenco chiuso:
// i tipi disponibili nel filtro si ricavano dagli spazi che esistono.
export type RoomType = string;

// Tipi che il servizio Rooms accetta in scrittura (enum fisso lato backend).
export const ROOM_TYPES: RoomType[] = ['Classroom', 'Laboratory', 'StudyRoom'];

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
  // Attrezzature dello spazio, con id e nome. Il servizio le invia sempre (anche vuote).
  equipment?: RoomEquipment[];
}

// Filtri della ricerca: tutti opzionali.
export interface RoomFilter {
  type?: RoomType | null;
  minCapacity?: number | null;
  maxCapacity?: number | null;
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

// Giorni nell'ordine in cui si mostrano nel modulo di gestione (la settimana parte dal lunedì).
export const WEEK_DAYS: DayOfWeek[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

// Edificio scelto nel modulo di gestione.
export interface BuildingOption {
  id: string;
  name: string;
}

// Edifici distinti che compaiono negli spazi, in ordine alfabetico.
export function buildingsOf(rooms: RoomSummary[]): BuildingOption[] {
  const byId = new Map(rooms.map((room) => [room.buildingId, room.buildingName]));
  return [...byId]
    .map(([id, name]) => ({ id, name }))
    .sort((a, b) => a.name.localeCompare(b.name, 'it'));
}

// Attrezzature distinte che compaiono negli spazi, in ordine alfabetico.
export function equipmentOf(rooms: RoomSummary[]): RoomEquipment[] {
  const byId = new Map(rooms.flatMap((room) => room.equipment ?? []).map((item) => [item.id, item]));
  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name, 'it'));
}

// Fascia di apertura inviata al servizio Rooms. Gli orari sono "HH:mm:ss".
export interface OpeningHoursInput {
  dayOfWeek: DayOfWeek;
  opensAt: string;
  closesAt: string;
}

// Corpo di POST /api/rooms e PUT /api/rooms/{id}. I giorni senza fascia sono chiusi.
export interface RoomWriteRequest {
  buildingId: string;
  name: string;
  type: RoomType;
  capacity: number;
  floor: number;
  isBookable: boolean;
  equipmentIds: string[];
  openingHours: OpeningHoursInput[];
}
