import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyReservationsResponse } from '../models/booking.model';

// Accesso al servizio Bookings attraverso il gateway.
@Injectable({ providedIn: 'root' })
export class BookingsService {
  private readonly http = inject(HttpClient);

  // Prenotazioni dell'utente autenticato, divise in future, passate e cancellate.
  getMine(): Observable<MyReservationsResponse> {
    return this.http.get<MyReservationsResponse>('/api/bookings/mine');
  }
}
