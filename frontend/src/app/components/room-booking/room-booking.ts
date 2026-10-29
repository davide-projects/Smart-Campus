import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

// Componenti di Angular Material
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepicker, MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

// Servizi e modelli
import {
  FreeRange,
  ReservationResponse,
  addDays,
  campusDateTime,
  campusTime,
  campusToday,
  formatTimeRange,
  halfHourSlots,
  pad,
  reservationTimeLabel,
  toMinutes,
} from '../../models/booking.model';
import { OpeningHours, RoomDetail, WEEK_DAYS } from '../../models/room.model';
import { ApiProblem, problemOf } from '../../services/api-problem';
import { BookingsService } from '../../services/bookings.service';
import { rejectionText } from '../../services/rejection-message';
import { RoomsService } from '../../services/rooms.service';

// Giorni prenotabili oltre oggi (assunzione A2).
const MAX_DAYS_AHEAD = 14;

// Durata massima di una prenotazione in minuti, come in Bookings.
const MAX_DURATION_MINUTES = 240;

// Lunghezza massima del motivo, come in Bookings.
export const MAX_PURPOSE_LENGTH = 200;

// Codice di motivo del conflitto sullo spazio: solo con questo si propone la disponibilità.
const ROOM_OCCUPIED_CODE = 'room-occupied';

// Ripiego per un servizio che non invia il codice: il conflitto si riconosce dal testo.
export const ROOM_CONFLICT_DETAIL = 'Lo spazio è già prenotato in questa fascia.';

export const GENERIC_ERROR = 'Non è stato possibile completare la prenotazione. Riprova più tardi.';

interface BookingForm {
  day: FormControl<Date | null>;
  start: FormControl<string | null>;
  end: FormControl<string | null>;
  purpose: FormControl<string>;
}

interface Confirmation {
  roomName: string;
  time: string;
}

@Component({
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  // Calendario in italiano solo su questa pagina.
  providers: [provideNativeDateAdapter(), { provide: MAT_DATE_LOCALE, useValue: 'it-IT' }],
  selector: 'app-room-booking',
  styleUrl: './room-booking.scss',
  templateUrl: './room-booking.html',
})
export class RoomBooking implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roomsService = inject(RoomsService);
  private readonly bookingsService = inject(BookingsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  protected readonly maxPurposeLength = MAX_PURPOSE_LENGTH;

  protected readonly roomId = signal<string | null>(null);
  protected readonly room = signal<RoomDetail | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isNotFound = signal(false);
  protected readonly loadFailed = signal(false);

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  private readonly rejectionCode = signal<string | null>(null);
  // Fasce libere proposte dopo un conflitto sullo spazio; null senza conflitto, vuoto se non ce ne sono.
  protected readonly freeSlots = signal<FreeRange[] | null>(null);
  protected readonly confirmation = signal<Confirmation | null>(null);
  protected readonly showAvailabilityLink = computed(
    () =>
      this.rejectionCode() === ROOM_OCCUPIED_CODE || this.errorMessage() === ROOM_CONFLICT_DETAIL,
  );

  // Giorni prenotabili: da oggi a oggi + 14 giorni, come date del campus.
  protected readonly minDay = campusToday();
  protected readonly maxDay = addDays(this.minDay, MAX_DAYS_AHEAD);

  protected readonly form = new FormGroup<BookingForm>({
    day: new FormControl<Date | null>(null, Validators.required),
    start: new FormControl<string | null>(null, Validators.required),
    end: new FormControl<string | null>(null, Validators.required),
    purpose: new FormControl('', {
      nonNullable: true,
      validators: Validators.maxLength(MAX_PURPOSE_LENGTH),
    }),
  });

  private readonly selectedDay = signal<Date | null>(null);
  private readonly selectedStart = signal<string | null>(null);

  // Orario di apertura del giorno scelto; null se non c'è un giorno o lo spazio è chiuso.
  protected readonly dayHours = computed(() => {
    const room = this.room();
    const day = this.selectedDay();
    return room && day ? openingHoursOn(room, day) : null;
  });

  // Inizio fino a mezz'ora prima della chiusura e, oggi, solo dopo l'ora attuale del campus; fine
  // da mezz'ora dopo l'apertura e, scelto l'inizio, solo dopo l'inizio. Il limite delle 4 ore e
  // l'ora che passa mentre la pagina è aperta restano del servizio.
  protected readonly startOptions = computed(() => {
    const hours = this.dayHours();
    const day = this.selectedDay();
    if (!hours || !day) {
      return [];
    }
    const starts = halfHourSlots(hours).slice(0, -1);
    if (day.getTime() !== campusToday().getTime()) {
      return starts;
    }
    // Gli orari «HH:mm» si confrontano come testo.
    const now = campusTime();
    return starts.filter((time) => time > now);
  });
  protected readonly endOptions = computed(() => {
    const hours = this.dayHours();
    const start = this.selectedStart();
    const ends = hours ? halfHourSlots(hours).slice(1) : [];
    // Gli orari «HH:mm» si confrontano come testo.
    return start === null ? ends : ends.filter((time) => time > start);
  });

  // Il calendario esclude i giorni senza orario di apertura.
  protected readonly isOpenDay = (day: Date | null): boolean => {
    const room = this.room();
    return !!day && !!room && openingHoursOn(room, day) !== null;
  };

  private readonly confirmationHeading = viewChild<ElementRef<HTMLElement>>('confirmationHeading');
  private readonly dayInput = viewChild<ElementRef<HTMLInputElement>>('dayInput');
  // Il pulsante è un componente di Material: si legge il suo elemento HTML per portarci il focus.
  private readonly confirmButton = viewChild('confirmButton', { read: ElementRef });

  constructor() {
    // Al cambio di giorno si azzerano gli orari che non sono più dentro l'apertura.
    this.form.controls.day.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((day) => {
        this.selectedDay.set(day);
        // Le fasce libere proposte erano del giorno di prima.
        this.freeSlots.set(null);
        const { start, end } = this.form.controls;
        if (end.value !== null && !this.endOptions().includes(end.value)) {
          end.reset(null);
        }
        if (start.value !== null && !this.startOptions().includes(start.value)) {
          start.reset(null);
        }
      });

    // Un nuovo inizio che supera la fine scelta la azzera, e la fine risulta obbligatoria.
    this.form.controls.start.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((start) => {
        this.selectedStart.set(start);
        const { end } = this.form.controls;
        if (end.value !== null && !this.endOptions().includes(end.value)) {
          end.setValue(null);
          end.markAsTouched();
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
    this.roomId.set(roomId);

    this.roomsService
      .getRoomById(roomId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (room) => {
          this.room.set(room);
          this.isLoading.set(false);
          this.applyQueryParams();
        },
        error: (error: unknown) => {
          this.isLoading.set(false);
          if (error instanceof HttpErrorResponse && error.status === 404) {
            this.isNotFound.set(true);
          } else {
            this.loadFailed.set(true);
          }
        },
      });
  }

  private applyQueryParams(): void {
    const queryDate = this.route.snapshot.queryParamMap.get('date');
    const queryStart = this.route.snapshot.queryParamMap.get('start');
    const queryEnd = this.route.snapshot.queryParamMap.get('end');

    if (queryDate) {
      const [y, m, d] = queryDate.split('-').map(Number);
      if (!isNaN(y) && !isNaN(m) && !isNaN(d)) {
        const date = new Date(y, m - 1, d);
        if (this.isOpenDay(date)) {
          this.form.controls.day.setValue(date);
        }
      }
    }
    if (queryStart && this.startOptions().includes(queryStart)) {
      this.form.controls.start.setValue(queryStart);
      if (queryEnd && this.endOptions().includes(queryEnd)) {
        this.form.controls.end.setValue(queryEnd);
      }
    }
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { day, start, end, purpose } = this.form.getRawValue();
    const trimmedPurpose = purpose.trim();
    this.submitting.set(true);
    this.clearRejection();

    this.bookingsService
      .create({
        roomId: this.roomId()!,
        start: campusDateTime(day!, start!),
        end: campusDateTime(day!, end!),
        purpose: trimmedPurpose === '' ? null : trimmedPurpose,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (reservation) => {
          this.submitting.set(false);
          this.confirmation.set(toConfirmation(reservation));
          this.focusAfterRender(() => this.confirmationHeading()?.nativeElement);
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          const problem = problemOf(error);
          this.errorMessage.set(rejectionMessage(problem));
          this.rejectionCode.set(problem.code);
          this.freeSlots.set(problem.code === ROOM_OCCUPIED_CODE ? problem.freeSlots : null);
        },
      });
  }

  // Il campo «Giorno» è di sola lettura: Invio e Alt+Freccia giù aprono il calendario.
  // preventDefault evita che Invio invii anche il modulo.
  protected openCalendar(event: Event, picker: MatDatepicker<Date>): void {
    event.preventDefault();
    picker.open();
  }

  // Etichetta di una fascia libera, es. «08:00–10:00», nell'ora del campus.
  protected freeSlotLabel(slot: FreeRange): string {
    return formatTimeRange(slot.start, slot.end, '–');
  }

  // Sceglie una fascia libera proposta dopo un conflitto: l'inizio è quello della fascia e la fine
  // mantiene la durata che lo studente aveva chiesto, se ci sta (altrimenti la fascia intera), al
  // massimo 4 ore. Rifiuto e proposte spariscono, perché riguardavano gli orari di prima, e il
  // focus passa al pulsante di conferma.
  protected chooseFreeSlot(slot: FreeRange): void {
    const { start, end } = this.form.controls;
    const slotStart = campusTime(new Date(slot.start));
    if (!this.startOptions().includes(slotStart)) {
      return;
    }

    const slotMinutes = (new Date(slot.end).getTime() - new Date(slot.start).getTime()) / 60_000;
    const requestedMinutes =
      start.value !== null && end.value !== null
        ? toMinutes(end.value) - toMinutes(start.value)
        : MAX_DURATION_MINUTES;
    const minutes = Math.min(
      requestedMinutes > 0 ? requestedMinutes : MAX_DURATION_MINUTES,
      slotMinutes,
      MAX_DURATION_MINUTES,
    );
    const endMinutes = toMinutes(slotStart) + minutes;

    start.setValue(slotStart);
    end.setValue(`${pad(Math.floor(endMinutes / 60))}:${pad(endMinutes % 60)}`);
    this.clearRejection();
    this.focusAfterRender(() => this.confirmButton()?.nativeElement);
  }

  // Dopo la conferma: modulo vuoto per lo stesso spazio, senza messaggi di rifiuto.
  protected bookAnother(): void {
    this.form.reset();
    this.clearRejection();
    this.confirmation.set(null);
    this.focusAfterRender(() => this.dayInput()?.nativeElement);
  }

  // Toglie il rifiuto mostrato: messaggio, codice e fasce libere proposte.
  private clearRejection(): void {
    this.errorMessage.set(null);
    this.rejectionCode.set(null);
    this.freeSlots.set(null);
  }

  // Il contenuto che aveva il focus sparisce: lo si porta sul nuovo contenuto.
  private focusAfterRender(target: () => HTMLElement | undefined): void {
    afterNextRender(() => target()?.focus(), { injector: this.injector });
  }
}

function toConfirmation(reservation: ReservationResponse): Confirmation {
  return { roomName: reservation.roomName, time: reservationTimeLabel(reservation) };
}

// 409 e 422 portano il motivo: il codice dà il messaggio in italiano con i suoi limiti e, senza
// codice, vale il detail del servizio. Il 401 porta il detail quando presente, ma la sessione viene
// comunque chiusa dall'interceptor. Ogni altro errore riceve il messaggio generico.
function rejectionMessage(problem: ApiProblem): string | null {
  if (problem.status === 409 || problem.status === 422) {
    return rejectionText(problem) ?? GENERIC_ERROR;
  }
  if (problem.status === 401) {
    return problem.detail !== null && problem.detail.trim() !== '' ? problem.detail : null;
  }
  return GENERIC_ERROR;
}

// Orario di apertura nel giorno della settimana della data scelta (data del calendario).
// WEEK_DAYS parte dal lunedì, Date.getDay() dalla domenica (0).
function openingHoursOn(room: RoomDetail, day: Date): OpeningHours | null {
  const weekDay = WEEK_DAYS[(day.getDay() + 6) % 7];
  const hours = room.openingHours.find((h) => h.dayOfWeek === weekDay);
  if (!hours || hours.isClosed || !hours.opensAt || !hours.closesAt) {
    return null;
  }
  return hours;
}
