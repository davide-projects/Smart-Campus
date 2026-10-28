import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

// Componenti di Angular Material
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

// Servizio e Modelli
import { AuthService } from '../../services/auth.service';
import { RoomsService } from '../../services/rooms.service';
import { BookingsService } from '../../services/bookings.service';
import { DailyAvailability } from '../../models/availability.model';
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
  private readonly bookingsService = inject(BookingsService);

  // Il pulsante «Modifica» compare solo al personale.
  protected readonly isStaff = inject(AuthService).isStaff;

  // Esposte al template HTML
  protected readonly roomTypeLabel = roomTypeLabel;
  protected readonly dayOfWeekLabel = dayOfWeekLabel;

  // Signal per memorizzare lo stato del componente
  protected readonly room = signal<RoomDetailData | null>(null);
  protected readonly isLoading = signal<boolean>(true);
  protected readonly isNotFound = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  // Data odierna in formato YYYY-MM-DD come valore iniziale
  protected readonly selectedDate = signal<string>(new Date().toISOString().slice(0, 10));

  // Stato della disponibilità per il giorno scelto
  protected readonly availability = signal<DailyAvailability | null>(null);
  protected readonly isLoadingAvailability = signal<boolean>(false);
  protected readonly availabilityErrorMessage = signal<string | null>(null);

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
        this.loadAvailability(data.id, this.selectedDate());
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

  // Estrae l'orario HH:mm dalla stringa ISO 8601 (es. "2026-11-03T08:30:00+01:00" -> "08:30")
  protected formatTime(isoDateTime: string): string {
    return isoDateTime.slice(11, 16);
  }

  // Scarica la disponibilità per la stanza e la data indicate
  protected loadAvailability(roomId: string, date: string): void {
    this.isLoadingAvailability.set(true);
    this.availabilityErrorMessage.set(null);

    this.bookingsService.getDailyAvailability(roomId, date).subscribe({
      next: (data) => {
        this.availability.set(data);
        this.isLoadingAvailability.set(false);
      },
      error: () => {
        this.isLoadingAvailability.set(false);
        this.availabilityErrorMessage.set(
          'Impossibile caricare la disponibilità per la data selezionata.'
        );
      },
    });
  }

  // Chiamato dall'HTML quando l'utente sceglie un nuovo giorno dall'input data
  protected onDateChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const newDate = input.value;
    const currentRoom = this.room();

    if (newDate && currentRoom) {
      this.selectedDate.set(newDate);
      this.loadAvailability(currentRoom.id, newDate);
    }
  }

  // Scorre la pagina dolcemente fino alla sezione della disponibilità
  protected scrollToAvailability(): void {
    const element = document.getElementById('availability-section');
    element?.scrollIntoView({ behavior: 'smooth' });
  }
}
