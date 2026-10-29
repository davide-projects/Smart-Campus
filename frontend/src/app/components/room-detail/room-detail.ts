import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';

// Componenti di Angular Material
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';

// Servizio e Modelli
import { DailyAvailability } from '../../models/availability.model';
import { addDays, campusToday, toIsoDate } from '../../models/booking.model';
import {
  RoomDetail as RoomDetailData,
  dayOfWeekLabel,
  roomTypeLabel,
} from '../../models/room.model';
import { AuthService } from '../../services/auth.service';
import { BookingsService } from '../../services/bookings.service';
import { RoomsService } from '../../services/rooms.service';
import { AvailabilityTimeline } from '../shared/availability-timeline/availability-timeline';
import { DeactivateRoomDialog, DeactivateRoomDialogData } from './deactivate-room-dialog';

@Component({
  imports: [
    AvailabilityTimeline,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
  ],
  providers: [provideNativeDateAdapter(), { provide: MAT_DATE_LOCALE, useValue: 'it-IT' }],
  selector: 'app-room-detail',
  styleUrl: './room-detail.scss',
  templateUrl: './room-detail.html',
})
export class RoomDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roomsService = inject(RoomsService);
  private readonly bookingsService = inject(BookingsService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  // Stato autenticazione e ruoli
  protected readonly isStaff = this.authService.isStaff;
  protected readonly isLoggedIn = this.authService.isLoggedIn;
  protected readonly currentUrl = this.router.url;

  // Esposte al template HTML
  protected readonly roomTypeLabel = roomTypeLabel;
  protected readonly dayOfWeekLabel = dayOfWeekLabel;

  // Signal per memorizzare lo stato del componente
  protected readonly room = signal<RoomDetailData | null>(null);
  protected readonly isLoading = signal<boolean>(true);
  protected readonly isNotFound = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isDeactivating = signal<boolean>(false);
  protected readonly deactivateError = signal<string | null>(null);

  // Limiti date nel fuso orario del campus (Europe/Rome): oggi fino a 14 giorni avanti
  protected readonly minDate = campusToday();
  protected readonly maxDate = addDays(this.minDate, 14);
  protected readonly selectedDate = signal<Date>(this.minDate);

  // Stato della disponibilità per il giorno scelto
  protected readonly availability = signal<DailyAvailability | null>(null);
  protected readonly isLoadingAvailability = signal<boolean>(false);
  protected readonly availabilityErrorMessage = signal<string | null>(null);

  // Pipeline reattiva per il caricamento disponibilità con switchMap
  private readonly dateRequests = new Subject<Date>();

  constructor() {
    this.dateRequests
      .pipe(
        tap(() => {
          this.isLoadingAvailability.set(true);
          this.availabilityErrorMessage.set(null);
        }),
        // switchMap: se l'utente cambia data rapidamente, la richiesta precedente viene annullata
        switchMap((date) => {
          const currentRoom = this.room();
          if (!currentRoom || !this.isLoggedIn()) {
            return of(null);
          }
          return this.bookingsService
            .getDailyAvailability(currentRoom.id, toIsoDate(date))
            .pipe(
              catchError(() => {
                this.availabilityErrorMessage.set(
                  'Impossibile caricare la disponibilità per la data selezionata.',
                );
                return of(null);
              }),
            );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((data) => {
        this.isLoadingAvailability.set(false);
        if (data) {
          this.availability.set(data);
        }
      });
  }

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
    this.deactivateError.set(null);

    this.roomsService.getRoomById(roomId).subscribe({
      next: (data) => {
        this.room.set(data);
        this.isLoading.set(false);
        if (this.isLoggedIn()) {
          this.dateRequests.next(this.selectedDate());
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 404) {
          this.isNotFound.set(true);
        } else {
          this.errorMessage.set(
            'Si è verificato un errore durante il caricamento dei dettagli dello spazio.',
          );
        }
      },
    });
  }

  // Invocato quando l'utente sceglie una nuova data dal MatDatepicker
  protected onDateChange(newDate: Date | null): void {
    if (newDate) {
      this.selectedDate.set(newDate);
      if (this.isLoggedIn()) {
        this.dateRequests.next(newDate);
      }
    }
  }

  // Apre la finestra di conferma che mostra il numero di prenotazioni future dello spazio.
  protected askDeactivation(): void {
    const current = this.room();

    if (!current) {
      return;
    }

    const dialogRef = this.dialog.open(DeactivateRoomDialog, {
      data: { roomId: current.id, roomName: current.name } satisfies DeactivateRoomDialogData,
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed === true) {
        this.deactivate(current.id);
      }
    });
  }

  // Dismette lo spazio e torna all'elenco; in caso di errore mostra un messaggio inline.
  private deactivate(roomId: string): void {
    this.isDeactivating.set(true);
    this.deactivateError.set(null);

    this.roomsService.deactivate(roomId).subscribe({
      next: () => {
        this.snackBar.open('Spazio dismesso', '', { duration: 3000 });
        this.router.navigate(['/rooms']);
      },
      error: () => {
        this.isDeactivating.set(false);
        this.deactivateError.set(
          'Si è verificato un errore durante la dismissione dello spazio. Riprova.'
        );
      },
    });
  }
}
