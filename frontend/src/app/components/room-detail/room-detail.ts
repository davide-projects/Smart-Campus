import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

// Componenti di Angular Material
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

// Servizio e Modelli
import { RoomsService } from '../../services/rooms.service';
import {
  RoomDetail as RoomDetailData,
  roomTypeLabel,
  dayOfWeekLabel,
} from '../../models/room.model';

@Component({
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  selector: 'app-room-detail',
  styleUrl: './room-detail.scss',
  templateUrl: './room-detail.html',
})
export class RoomDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roomsService = inject(RoomsService);

  // Esposte al template HTML
  protected readonly roomTypeLabel = roomTypeLabel;
  protected readonly dayOfWeekLabel = dayOfWeekLabel;

  // Signal per memorizzare lo stato del componente
  protected readonly room = signal<RoomDetailData | null>(null);
  protected readonly isLoading = signal<boolean>(true);
  protected readonly isNotFound = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const roomId = this.route.snapshot.paramMap.get('id');

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

    this.roomsService.getRoomById(roomId).subscribe({
      next: (data) => {
        this.room.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 404) {
          this.isNotFound.set(true);
        } else {
          this.errorMessage.set(
            'Si è verificato un errore durante il caricamento dei dettagli dello spazio.'
          );
        }
      },
    });
  }
}
