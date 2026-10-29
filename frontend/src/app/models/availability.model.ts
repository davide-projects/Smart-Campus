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
