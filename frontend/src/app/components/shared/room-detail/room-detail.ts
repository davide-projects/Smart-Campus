import { Component, inject, input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';

//angular material component principali
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

//Services e models 
import { RoomService } from '../../../services/room.service';
import { RoomDetail as RoomDetailData } from '../../../models/room.model';

@Component({
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule
  ],
  selector: 'app-room-detail',
  styleUrl: './room-detail.scss',
  templateUrl: './room-detail.html',
})
export class RoomDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roomService = inject(RoomService);

  //signal per memorizzare i dettagli della stanza
  protected readonly room = signal<RoomDetailData | null>(null);
  protected readonly isLoading = signal<boolean>(true);
  protected readonly isNotFound = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const roomId = this.route.snapshot.paramMap.get('id'); //catturo stato rotta

    if (!roomId) {
      this.isNotFound.set(true);
      this.isLoading.set(false);
      return;
    }
    this.loadRoom(roomId);
    
  }

  private loadRoom(roomId: string): void {
    this.isLoading.set(true);
    this.isNotFound.set(false);
    this.errorMessage.set(null);

    //chiamata http 404 se la stanza non esiste, altrimenti carica i dettagli della stanza 200ok 
    this.roomService.getRoomById(roomId).subscribe({
      next: (data) => {
        this.room.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 404) {
          this.isNotFound.set(true);
        } else {
          this.errorMessage.set('Sì è verificato un errore durante il caricamento dei dettagli dello spazio.');
        }
      },
    });
  }

  //protected perché voglio che sia accessibile a room-detail.html
  protected getDayName(dayOfWeek: number): string {
    const days = [
      'Domenica',
      'Lunedì',
      'Martedì',
      'Mercoledì',
      'Giovedì',
      'Venerdì',
      'Sabato'
    ];
    return days[dayOfWeek] ?? 'Giorno non valido';
  }

  
  //genero etichetta chiave valore associa il tipo in inglese con il rispettivo in italiano
  protected getTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      Classroom: 'Aula',
      Laboratory: 'Laboratorio',
      StudyRoom: 'Sala di studio'
    };
    return labels[type] ?? type; //se la trova restituisce l'etichetta altrimenti quella originale
  }
}
