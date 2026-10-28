import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MyReservationItem, reservationTimeLabel } from '../../../models/booking.model';
import { problemOf } from '../../../services/api-problem';
import { BookingsService } from '../../../services/bookings.service';

export const CANCEL_FAILED_MESSAGE =
  'Non è stato possibile cancellare la prenotazione. Riprova più tardi.';

// Errori di cui si mostra il messaggio del servizio: non consentita, inesistente, già iniziata.
const STATUSES_WITH_DETAIL = [403, 404, 422];

// Conferma della cancellazione. Si chiude con true solo dopo il 204 del servizio.
@Component({
  imports: [MatButtonModule, MatDialogModule],
  selector: 'app-cancel-booking-dialog',
  styleUrl: './cancel-booking-dialog.scss',
  templateUrl: './cancel-booking-dialog.html',
})
export class CancelBookingDialog {
  private readonly dialogRef = inject<MatDialogRef<CancelBookingDialog, boolean>>(MatDialogRef);
  private readonly bookingsService = inject(BookingsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reservation = inject<MyReservationItem>(MAT_DIALOG_DATA);

  protected readonly roomName = this.reservation.roomName;
  protected readonly buildingName = this.reservation.buildingName ?? '—';
  protected readonly time = reservationTimeLabel(this.reservation);

  protected readonly pending = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected dismiss(): void {
    if (!this.pending()) {
      this.dialogRef.close();
    }
  }

  protected confirm(): void {
    // Una sola richiesta anche con doppio clic: i pulsanti restano attivabili per non perdere il focus.
    if (this.pending()) {
      return;
    }
    this.pending.set(true);
    this.errorMessage.set(null);
    // Esc e clic fuori non chiudono il dialog finché la richiesta è in corso.
    this.dialogRef.disableClose = true;

    this.bookingsService
      .cancel(this.reservation.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (error: unknown) => {
          const problem = problemOf(error);
          // Con 401 l'interceptor ha già chiuso la sessione e portato al login.
          if (problem.status === 401) {
            this.dialogRef.close();
            return;
          }
          this.pending.set(false);
          this.dialogRef.disableClose = false;
          this.errorMessage.set(
            STATUSES_WITH_DETAIL.includes(problem.status) && problem.detail
              ? problem.detail
              : CANCEL_FAILED_MESSAGE,
          );
        },
      });
  }
}
