import { inject, Injectable } from '@angular/core'; 
import { HttpClient } from '@angular/common/http'; //permette di fare richieste HTTP al backend
import { Observable } from 'rxjs';
import { RoomDetail } from '../models/room.model'; 

//dependency injection per ottenere un'istanza di HttpClient 
@Injectable({
  providedIn: 'root'
})
export class RoomService {
  private http = inject(HttpClient); //inject nell'istanza di RoomService 
  private readonly baseUrl = 'api/rooms'; //base URL for the rooms API'

  getRoomById(id: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`${this.baseUrl}/${id}`); //esegue una get request
  }
}

