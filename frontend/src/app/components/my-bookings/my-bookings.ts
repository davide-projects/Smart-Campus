import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import {
  MyReservationItem,
  MyReservationsResponse,
  organizerLabel,
  reservationTimeLabel,
} from '../../models/booking.model';
import { BookingsService } from '../../services/bookings.service';

// Voce come la mostra la pagina: organizer è null sulle prenotazioni singole.
interface ReservationView {
  id: string;
  roomName: string;
  buildingName: string;
  time: string;
  organizer: string | null;
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

  private readonly reservations = signal<MyReservationsResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);

  // Le voci restano nell'ordine ricevuto dall'API.
  protected readonly tabs = computed<ReservationTab[]>(() => {
    const data = this.reservations();
    if (!data) {
      return [];
    }
    return [
      {
        label: 'Future',
        items: data.upcoming.map(toView),
        emptyMessage: 'Non hai prenotazioni future.',
        showSearchLink: true,
      },
      {
        label: 'Passate',
        items: data.past.map(toView),
        emptyMessage: 'Non hai prenotazioni passate.',
        showSearchLink: false,
      },
      {
        label: 'Cancellate',
        items: data.cancelled.map(toView),
        emptyMessage: 'Non hai prenotazioni cancellate.',
        showSearchLink: false,
      },
    ];
  });

  constructor() {
    this.bookingsService
      .getMine()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (data) => {
          this.reservations.set(data);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          // Con 401 l'interceptor ha già chiuso la sessione e portato al login.
          if (!(error instanceof HttpErrorResponse && error.status === 401)) {
            this.loadFailed.set(true);
          }
        },
      });
  }
}

function toView(item: MyReservationItem): ReservationView {
  return {
    id: item.id,
    roomName: item.roomName,
    buildingName: item.buildingName ?? '—',
    time: reservationTimeLabel(item),
    organizer: item.isGroup ? organizerLabel(item) : null,
  };
}
