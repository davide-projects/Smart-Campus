import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { StudentSummary } from '../models/user.model';

// Ricerca degli studenti nel servizio Identity attraverso il gateway.
@Injectable({ providedIn: 'root' })
export class StudentsService {
  private readonly http = inject(HttpClient);

  // Studenti il cui nome visualizzato o username contiene il testo (al massimo 10, escluso chi
  // cerca). Il servizio accetta solo testi di almeno 2 caratteri.
  search(text: string): Observable<StudentSummary[]> {
    const params = new HttpParams().set('search', text);
    return this.http.get<StudentSummary[]>('/api/identity/students', { params });
  }
}
