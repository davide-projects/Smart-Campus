import { Component, inject, OnInit, signal } from '@angular/core';

// Componenti di Angular Material
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

// Servizio
import { BookingsService } from '../../services/bookings.service';

// Dati passati alla finestra: lo spazio che si vuole dismettere.
export interface DeactivateRoomDialogData {
  roomId: string;
  roomName: string;
}

// Finestra di conferma della dismissione: mostra quante prenotazioni future ha lo spazio
// (GET /api/bookings/rooms/{id}/future-reservations-count) prima di confermare l'operazione.
@Component({
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  selector: 'app-deactivate-room-dialog',
  templateUrl: './deactivate-room-dialog.html',
})
export class DeactivateRoomDialog implements OnInit {
  private readonly dialogRef = inject<MatDialogRef<DeactivateRoomDialog>>(MatDialogRef);
  private readonly bookingsService = inject(BookingsService);

  // Dati iniettati da chi apre la finestra.
  protected readonly data = inject<DeactivateRoomDialogData>(MAT_DIALOG_DATA);

  // Signal per memorizzare lo stato della finestra
  protected readonly isLoading = signal<boolean>(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly count = signal<number | null>(null);

  ngOnInit(): void {
    this.loadCount();
  }

  // Il pulsante «Dismetti» è attivo solo quando il conteggio è stato caricato.
  protected canConfirm(): boolean {
    return !this.isLoading() && this.errorMessage() === null;
  }

  // Ricarica il conteggio (chiamato all'apertura e dopo un errore).
  protected retry(): void {
    this.loadCount();
  }

  // Conferma la dismissione e chiude la finestra comunicando l'esito.
  protected confirm(): void {
    if (!this.canConfirm()) {
      return;
    }
    this.dialogRef.close(true);
  }

  // Annulla l'operazione.
  protected cancel(): void {
    this.dialogRef.close(false);
  }

  private loadCount(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.bookingsService.futureReservationsCount(this.data.roomId).subscribe({
      next: (count) => {
        this.count.set(count);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set(
          'Impossibile recuperare il numero di prenotazioni future. Controlla la connessione e riprova.'
        );
      },
    });
  }
}
