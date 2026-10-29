import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import { AvailableRoomResponse } from '../../models/availability.model';
import {
  addDays,
  campusToday,
  defaultSearchSlot,
  halfHourSlots,
  toIsoDate,
} from '../../models/booking.model';
import { ROOM_TYPES, roomTypeLabel } from '../../models/room.model';
import { BookingsService } from '../../services/bookings.service';

interface SearchForm {
  date: FormControl<Date | null>;
  start: FormControl<string | null>;
  end: FormControl<string | null>;
  type: FormControl<string | null>;
  minCapacity: FormControl<number | null>;
}

@Component({
  selector: 'app-available-rooms',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
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
  templateUrl: './available-rooms.html',
  styleUrl: './available-rooms.scss',
})
export class AvailableRooms implements OnInit {
  private readonly bookingsService = inject(BookingsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly minDay = campusToday();
  protected readonly maxDay = addDays(this.minDay, 14);

  protected readonly roomTypes = ROOM_TYPES;
  protected readonly roomTypeLabel = roomTypeLabel;

  protected readonly timeSlots = halfHourSlots('08:00', '20:00');
  private readonly initialSlot = defaultSearchSlot();

  protected readonly results = signal<AvailableRoomResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly hasSearched = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly lastSearchedParams = signal<{
    date: string;
    start: string;
    end: string;
  } | null>(null);

  protected readonly form = new FormGroup<SearchForm>({
    date: new FormControl<Date | null>(campusToday(), Validators.required),
    start: new FormControl<string | null>(this.initialSlot.start, Validators.required),
    end: new FormControl<string | null>(this.initialSlot.end, Validators.required),
    type: new FormControl<string | null>(null),
    minCapacity: new FormControl<number | null>(null, [Validators.min(1)]),
  });

  protected readonly selectedDay = signal<Date | null>(campusToday());
  protected readonly selectedStart = signal<string | null>(this.initialSlot.start);

  protected readonly startOptions = this.timeSlots.slice(0, -1);

  protected readonly endOptions = computed(() => {
    const start = this.selectedStart();
    if (!start) {
      return this.timeSlots.slice(1);
    }
    return this.timeSlots.filter((time) => time > start);
  });

  ngOnInit(): void {
    this.form.controls.date.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((date) => this.selectedDay.set(date));

    // Quando cambia l'ora di inizio, aggiorna il signal per ricalcolare endOptions
    // e se l'ora di fine non è più valida viene aggiornata alla mezz'ora successiva.
    this.form.controls.start.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((start) => {
        this.selectedStart.set(start);
        const end = this.form.controls.end.value;
        if (start && end && end <= start) {
          const nextSlot = this.timeSlots.find((t) => t > start);
          this.form.controls.end.setValue(nextSlot ?? null);
        }
      });
  }

  protected search(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { date, start, end, type, minCapacity } = this.form.value;
    if (!date || !start || !end) {
      return;
    }

    if (end <= start) {
      this.errorMessage.set('L\'ora di fine deve essere successiva a quella di inizio.');
      return;
    }

    const isoDate = toIsoDate(date);
    this.lastSearchedParams.set({
      date: isoDate,
      start,
      end,
    });

    this.loading.set(true);
    this.errorMessage.set(null);

    this.bookingsService
      .searchAvailableRooms({
        date: isoDate,
        start,
        end,
        type: type || null,
        minCapacity: minCapacity || null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rooms) => {
          this.results.set(rooms);
          this.loading.set(false);
          this.hasSearched.set(true);
        },
        error: () => {
          this.errorMessage.set('Non è stato possibile caricare gli spazi liberi. Riprova più tardi.');
          this.loading.set(false);
          this.hasSearched.set(true);
        },
      });
  }
}
