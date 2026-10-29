import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabGroup, MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import {
  MyReservationItem,
  MyReservationsResponse,
  membersLabel,
  organizerLabel,
  reservationTimeLabel,
} from '../../models/booking.model';
import { BookingsService } from '../../services/bookings.service';
import { CancelBookingDialog } from './cancel-booking-dialog/cancel-booking-dialog';

// Voce come la mostra la pagina: organizer e members sono null sulle prenotazioni singole.
interface ReservationView {
  item: MyReservationItem;
  roomName: string;
  buildingName: string;
  time: string;
  organizer: string | null;
  members: string | null;
  // Solo l'organizzatore, prima dell'inizio, vede «Cancella».
  canCancel: boolean;
  cancelLabel: string;
}

interface ReservationTab {
  label: string;
  items: ReservationView[];
  emptyMessage: string;
  // Solo la scheda «Future» rimanda alla ricerca degli spazi.
  showSearchLink: boolean;
}

@Component({
  imports: [MatButtonModule, MatCardModule, MatProgressBarModule, MatTabsModule, RouterLink],
  selector: 'app-my-bookings',
  styleUrl: './my-bookings.scss',
  templateUrl: './my-bookings.html',
})
export class MyBookings {
  private readonly bookingsService = inject(BookingsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly tabGroup = viewChild(MatTabGroup, { read: ElementRef });

  private readonly reservations = signal<MyReservationsResponse | null>(null);
  // Ora del dispositivo all'ultimo caricamento: decide quali voci future si possono cancellare.
  private readonly loadedAt = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  // Ricaricamento fallito dopo una cancellazione: le schede restano, con un avviso sopra.
  protected readonly reloadFailed = signal(false);

  // Le voci restano nell'ordine ricevuto dall'API.
  protected readonly tabs = computed<ReservationTab[]>(() => {
    const data = this.reservations();
    if (!data) {
      return [];
    }
    const now = this.loadedAt();
    return [
      {
        label: 'Future',
        items: data.upcoming.map((item) =>
          toView(item, item.isOrganizer && Date.parse(item.start) > now),
        ),
        emptyMessage: 'Non hai prenotazioni future.',
        showSearchLink: true,
      },
      {
        label: 'Passate',
        items: data.past.map((item) => toView(item, false)),
        emptyMessage: 'Non hai prenotazioni passate.',
        showSearchLink: false,
      },
      {
        label: 'Cancellate',
        items: data.cancelled.map((item) => toView(item, false)),
        emptyMessage: 'Non hai prenotazioni cancellate.',
        showSearchLink: false,
      },
    ];
  });

  constructor() {
    this.load();
  }

  protected openCancelDialog(item: MyReservationItem): void {
    this.dialog
      .open<CancelBookingDialog, MyReservationItem, boolean>(CancelBookingDialog, { data: item })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((cancelled) => {
        if (cancelled !== true) {
          return;
        }
        this.snackBar.open('Prenotazione cancellata.', undefined, { duration: 5000 });
        // Il pulsante che ha aperto il dialog sparisce con il ricaricamento: il focus va su «Future».
        this.focusUpcomingTab();
        this.load();
      });
  }

  // Il primo caricamento mostra l'indicatore al posto delle schede; il ricaricamento le lascia visibili.
  private load(): void {
    const initial = this.reservations() === null;
    this.reloadFailed.set(false);
    this.bookingsService
      .getMine()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.loadedAt.set(Date.now());
          this.reservations.set(data);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          // Con 401 l'interceptor ha già chiuso la sessione e portato al login.
          if (error instanceof HttpErrorResponse && error.status === 401) {
            return;
          }
          if (initial) {
            this.loadFailed.set(true);
          } else {
            this.reloadFailed.set(true);
          }
        },
      });
  }

  private focusUpcomingTab(): void {
    const label = this.tabGroup()?.nativeElement.querySelector('[role="tab"]');
    label?.focus();
  }
}

function toView(item: MyReservationItem, canCancel: boolean): ReservationView {
  const time = reservationTimeLabel(item);
  return {
    item,
    roomName: item.roomName,
    buildingName: item.buildingName ?? '—',
    time,
    organizer: item.isGroup ? organizerLabel(item) : null,
    members: membersLabel(item),
    canCancel,
    cancelLabel: `Cancella la prenotazione di ${item.roomName}, ${time}`,
  };
}
