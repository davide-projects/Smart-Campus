import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DailyAvailability } from '../models/availability.model';
import { MyReservationsResponse } from '../models/booking.model';

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
}
