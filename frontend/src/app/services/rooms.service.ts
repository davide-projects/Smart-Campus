import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  BuildingOption,
  RoomDetail,
  RoomEquipment,
  RoomFilter,
  RoomSummary,
  RoomWriteRequest,
  buildingsOf,
  equipmentOf,
} from '../models/room.model';

// Scelte offerte dal modulo di creazione e modifica di uno spazio.
export interface RoomFormOptions {
  buildings: BuildingOption[];
  equipment: RoomEquipment[];
}

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

  // Recupera il dettaglio di uno spazio tramite il suo identificativo.
  getRoomById(id: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`/api/rooms/${id}`);
  }

  // Edifici e attrezzature tra cui scegliere nel modulo di gestione. Rooms non ha un elenco
  // né degli uni né delle altre: si ricavano dagli spazi attivi con una sola ricerca, quindi
  // compaiono solo gli edifici con almeno uno spazio e le attrezzature assegnate ad almeno uno.
  listFormOptions(): Observable<RoomFormOptions> {
    return this.search().pipe(
      map((rooms) => ({ buildings: buildingsOf(rooms), equipment: equipmentOf(rooms) })),
    );
  }

  // Crea uno spazio (solo personale). Risponde con l'identificativo del nuovo spazio.
  create(request: RoomWriteRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/rooms', request);
  }

  // Sostituisce i dati di uno spazio attivo (solo personale): la PUT riscrive anche attrezzature e orari.
  update(id: string, request: RoomWriteRequest): Observable<void> {
    return this.http.put<void>(`/api/rooms/${id}`, request);
  }
}
