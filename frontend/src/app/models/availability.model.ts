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
