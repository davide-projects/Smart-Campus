import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import {
  ReservationListItem,
  ReservationStatus,
  campusToday,
  formatTimeRange,
  toIsoDate,
} from '../../models/booking.model';
import { RoomSummary } from '../../models/room.model';
import { BookingsService } from '../../services/bookings.service';
import { RoomsService } from '../../services/rooms.service';

interface FilterForm {
  roomId: FormControl<string>;
  date: FormControl<Date | null>;
}

@Component({
  selector: 'app-room-reservations',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './room-reservations.html',
  styleUrl: './room-reservations.scss',
})
export class RoomReservations implements OnInit {
  private readonly bookingsService = inject(BookingsService);
  private readonly roomsService = inject(RoomsService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly rooms = signal<RoomSummary[]>([]);
  protected readonly reservations = signal<ReservationListItem[]>([]);
  protected readonly loadingRooms = signal(true);
  protected readonly loadingReservations = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hasSearched = signal(false);

  protected readonly form = new FormGroup<FilterForm>({
    roomId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    date: new FormControl<Date | null>(campusToday(), { validators: [Validators.required] }),
  });

  ngOnInit(): void {
    const queryRoomId = this.route.snapshot.queryParamMap.get('roomId');
    if (queryRoomId) {
      this.form.controls.roomId.setValue(queryRoomId);
    }

    this.roomsService
      .search()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rooms) => {
          this.rooms.set(rooms);
          this.loadingRooms.set(false);

          if (this.form.valid) {
            this.loadReservations();
          }
        },
        error: () => {
          this.loadingRooms.set(false);
          this.errorMessage.set('Non è stato possibile caricare l\'elenco degli spazi.');
        },
      });

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.form.valid) {
          this.loadReservations();
        }
      });
  }

  protected loadReservations(): void {
    const roomId = this.form.controls.roomId.value;
    const date = this.form.controls.date.value;

    if (!roomId || !date) {
      return;
    }

    this.loadingReservations.set(true);
    this.errorMessage.set(null);

    this.bookingsService
      .getReservations(roomId, toIsoDate(date))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          this.reservations.set(items);
          this.loadingReservations.set(false);
          this.hasSearched.set(true);
        },
        error: () => {
          this.errorMessage.set('Non è stato possibile caricare le prenotazioni. Riprova più tardi.');
          this.loadingReservations.set(false);
          this.hasSearched.set(true);
        },
      });
  }

  protected formatTime(start: string, end: string): string {
    return formatTimeRange(start, end);
  }

  protected statusLabel(status: ReservationStatus): string {
    return status === 'Confirmed' ? 'Confermata' : 'Cancellata';
  }

  protected participantsLabel(count: number): string {
    return count === 1 ? '1 persona' : `${count} persone`;
  }
}
