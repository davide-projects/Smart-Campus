import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RoomFilter, RoomSummary } from './room.model';

// Accesso al servizio Rooms attraverso il gateway.
@Injectable({ providedIn: 'root' })
export class RoomsService {
  private readonly http = inject(HttpClient);

  // Cerca gli spazi attivi; i filtri vuoti non vengono inviati.
  search(filter: RoomFilter = {}): Observable<RoomSummary[]> {
    let params = new HttpParams();

    if (filter.type) {
      params = params.set('type', filter.type);
    }
    if (filter.minCapacity != null) {
      params = params.set('minCapacity', filter.minCapacity);
    }
    if (filter.buildingId) {
      params = params.set('buildingId', filter.buildingId);
    }

    return this.http.get<RoomSummary[]>('/api/rooms', { params });
  }
}
