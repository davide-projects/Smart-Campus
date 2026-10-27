// Tipi di spazio come li invia il servizio Rooms (enum serializzato come stringa).
export type RoomType = 'Classroom' | 'Laboratory' | 'StudyRoom';

// Etichette italiane mostrate all'utente per ogni tipo di spazio.
export const ROOM_TYPE_LABELS: Record<RoomType, string> = {
  Classroom: 'Aula',
  Laboratory: 'Laboratorio',
  StudyRoom: 'Sala studio',
};

// Elemento della risposta di GET /api/rooms.
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
