import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { DailyAvailability, AvailabilitySearchFilter, AvailableRoomResponse } from '../models/availability.model';
import {
  CheckInResponse,
  CreateReservationRequest,
  FutureReservationsCountResponse,
  MyReservationsResponse,
  ReservationResponse,
} from '../models/booking.model';

// Accesso al servizio Bookings attraverso il gateway.
@Injectable({ providedIn: 'root' })
export class BookingsService {
  private readonly http = inject(HttpClient);

  // Prenotazioni dell'utente autenticato, divise in future, passate e cancellate.
  getMine(): Observable<MyReservationsResponse> {
    return this.http.get<MyReservationsResponse>('/api/bookings/mine');
  }

  // Recupera la disponibilità giornaliera di uno spazio per una specifica data.
  getDailyAvailability(roomId: string, date: string): Observable<DailyAvailability> {
    const params = new HttpParams()
      .set('roomId', roomId)
      .set('date', date);
    return this.http.get<DailyAvailability>('/api/bookings/availability', { params });
  }

  // Cancella una prenotazione: la riga resta, con stato «cancellata». Risposta 204 senza corpo.
  cancel(id: string): Observable<void> {
    return this.http.delete<void>(`/api/bookings/${encodeURIComponent(id)}`);
  }

  // Registra il check-in di una prenotazione, valido per tutto il gruppo. Richiesta senza corpo.
  checkIn(id: string): Observable<CheckInResponse> {
    return this.http.post<CheckInResponse>(
      `/api/bookings/${encodeURIComponent(id)}/check-in`,
      null,
    );
  }

  // Crea una prenotazione singola confermata a nome dell'utente autenticato.
  create(request: CreateReservationRequest): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>('/api/bookings', request);
  }

  // Numero di prenotazioni future di uno spazio, mostrato prima di dismetterlo (solo personale).
  futureReservationsCount(roomId: string): Observable<number> {
    return this.http
      .get<FutureReservationsCountResponse>(`/api/bookings/rooms/${roomId}/future-reservations-count`)
      .pipe(map((response) => response.count));
  }

  // Cerca gli spazi liberi in una data e fascia oraria, con filtri opzionali per tipo e capienza.
  searchAvailableRooms(filter: AvailabilitySearchFilter): Observable<AvailableRoomResponse[]> {
    let params = new HttpParams()
      .set('date', filter.date)
      .set('start', filter.start)
      .set('end', filter.end);

    if (filter.type) {
      params = params.set('type', filter.type);
    }
    if (filter.minCapacity != null && filter.minCapacity > 0) {
      params = params.set('minCapacity', filter.minCapacity.toString());
    }

    return this.http.get<AvailableRoomResponse[]>('/api/bookings/availability/search', { params });
  }
}
