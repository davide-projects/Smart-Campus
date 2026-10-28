import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DailyAvailability } from '../models/availability.model';
import {
  CreateReservationRequest,
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

  // Crea una prenotazione singola confermata a nome dell'utente autenticato.
  create(request: CreateReservationRequest): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>('/api/bookings', request);
  }
}
