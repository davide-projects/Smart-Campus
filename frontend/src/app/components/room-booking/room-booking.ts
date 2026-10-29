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
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

// Componenti di Angular Material
import {
  MatAutocompleteModule,
  MatAutocompleteSelectedEvent,
  MatAutocompleteTrigger,
} from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepicker, MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';

// Servizi e modelli
import { DailyAvailability } from '../../models/availability.model';
import {
  FreeRange,
  MAX_MEMBERS,
  ReservationResponse,
  addDays,
  campusDateTime,
  campusTime,
  campusToday,
  formatTimeRange,
  halfHourSlots,
  pad,
  reservationTimeLabel,
  toIsoDate,
  toMinutes,
} from '../../models/booking.model';
import { OpeningHours, RoomDetail, WEEK_DAYS } from '../../models/room.model';
import { StudentSummary } from '../../models/user.model';
import { AuthService } from '../../services/auth.service';
import { ApiProblem, problemOf } from '../../services/api-problem';
import { BookingsService } from '../../services/bookings.service';
import { rejectionText } from '../../services/rejection-message';
import { RoomsService } from '../../services/rooms.service';
import { StudentsService } from '../../services/students.service';
import { AvailabilityTimeline } from '../shared/availability-timeline/availability-timeline';

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

// Attesa dopo l'ultimo tasto prima di cercare gli studenti, e lunghezza minima del testo.
const MEMBER_SEARCH_DEBOUNCE_MS = 300;
const MIN_SEARCH_LENGTH = 2;

interface BookingForm {
  day: FormControl<Date | null>;
  start: FormControl<string | null>;
  end: FormControl<string | null>;
  purpose: FormControl<string>;
}

interface Confirmation {
  roomName: string;
  time: string;
  participants: number;
}

// Esito dell'ultima ricerca degli studenti, per il testo cercato (già senza spazi ai bordi).
type MemberSearchResult =
  | { text: string; status: 'done'; students: StudentSummary[] }
  | { text: string; status: 'error' }
  | { text: string; status: 'idle' };

@Component({
  imports: [
    AvailabilityTimeline,
    ReactiveFormsModule,
    RouterLink,
    MatAutocompleteModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
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
  private readonly studentsService = inject(StudentsService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  protected readonly maxPurposeLength = MAX_PURPOSE_LENGTH;
  protected readonly maxMembers = MAX_MEMBERS;

  // Lo Staff non crea prenotazioni di gruppo: il campo «Membri del gruppo» non compare.
  protected readonly isStaff = this.auth.isStaff;

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

  // Membri scelti: si scelgono tra le proposte, non si scrivono, quindi stanno fuori dal FormGroup.
  protected readonly members = signal<StudentSummary[]>([]);
  protected readonly participants = computed(() => 1 + this.members().length);
  protected readonly overCapacity = computed(() => {
    const room = this.room();
    return !!room && this.participants() > room.capacity;
  });
  protected readonly membersFull = computed(() => this.members().length >= MAX_MEMBERS);

  // Testo scritto nel campo di ricerca e risultato dell'ultima ricerca inviata.
  private readonly memberSearchText = signal('');
  private readonly memberSearchResult = signal<MemberSearchResult>({ text: '', status: 'idle' });
  private readonly memberSearchTerms = new Subject<string>();

  // Il risultato vale solo se riguarda il testo che c'è ora nel campo.
  private readonly currentSearchResult = computed(() => {
    const result = this.memberSearchResult();
    const text = this.memberSearchText().trim();
    return text.length >= MIN_SEARCH_LENGTH && result.text === text ? result : null;
  });
  // Proposte senza gli studenti già scelti; null finché non c'è una risposta per il testo attuale.
  protected readonly memberSuggestions = computed(() => {
    const result = this.currentSearchResult();
    if (result?.status !== 'done') {
      return null;
    }
    const chosen = new Set(this.members().map((member) => member.id));
    return result.students.filter((student) => !chosen.has(student.id));
  });
  protected readonly memberSearchFailed = computed(
    () => this.currentSearchResult()?.status === 'error',
  );

  private readonly selectedDay = signal<Date | null>(null);
  private readonly selectedStart = signal<string | null>(null);

  // Disponibilità del giorno scelto, mostrata come linea del tempo sopra gli orari: chi prenota
  // vede le fasce libere senza tornare al dettaglio dello spazio. Se non si carica, la
  // prenotazione resta possibile: l'ultima parola sui conflitti è comunque di Bookings.
  protected readonly hasDay = computed(() => this.selectedDay() !== null);
  protected readonly availability = signal<DailyAvailability | null>(null);
  protected readonly isLoadingAvailability = signal(false);
  protected readonly availabilityFailed = signal(false);

  // `keepCurrent`: l'aggiornamento dopo un rifiuto non svuota la linea del tempo mentre ricarica.
  private readonly availabilityRequests = new Subject<{ day: Date | null; keepCurrent: boolean }>();

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
  private readonly memberInput = viewChild<ElementRef<HTMLInputElement>>('memberInput');
  // Il pulsante è un componente di Material: si legge il suo elemento HTML per portarci il focus.
  private readonly confirmButton = viewChild('confirmButton', { read: ElementRef });

  constructor() {
    // switchMap: scegliendo un altro giorno la richiesta precedente viene annullata.
    this.availabilityRequests
      .pipe(
        tap(({ day, keepCurrent }) => {
          this.availabilityFailed.set(false);
          if (!keepCurrent) {
            this.availability.set(null);
            this.isLoadingAvailability.set(day !== null);
          }
        }),
        switchMap(({ day }) => {
          const roomId = this.roomId();
          if (day === null || roomId === null) {
            return of(null);
          }
          return this.bookingsService.getDailyAvailability(roomId, toIsoDate(day)).pipe(
            catchError(() => {
              this.availabilityFailed.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((availability) => {
        this.isLoadingAvailability.set(false);
        // Un errore durante l'aggiornamento lascia visibile l'ultima linea del tempo caricata.
        if (availability !== null || !this.availabilityFailed()) {
          this.availability.set(availability);
        }
      });

    // Ricerca degli studenti: da 2 caratteri, dopo una pausa nella scrittura, e solo l'ultima
    // richiesta conta. Un errore non interrompe le ricerche successive; il 401 chiude la sessione
    // nell'interceptor, quindi non mostra il messaggio.
    this.memberSearchTerms
      .pipe(
        map((text) => text.trim()),
        debounceTime(MEMBER_SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        switchMap((text) =>
          text.length < MIN_SEARCH_LENGTH
            ? of<MemberSearchResult>({ text, status: 'idle' })
            : this.studentsService.search(text).pipe(
                map((students): MemberSearchResult => ({ text, status: 'done', students })),
                catchError((error: unknown) =>
                  of<MemberSearchResult>(
                    error instanceof HttpErrorResponse && error.status === 401
                      ? { text, status: 'idle' }
                      : { text, status: 'error' },
                  ),
                ),
              ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.memberSearchResult.set(result));

    // Al cambio di giorno si azzerano gli orari che non sono più dentro l'apertura.
    this.form.controls.day.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((day) => {
        this.selectedDay.set(day);
        this.availabilityRequests.next({ day, keepCurrent: false });
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
    // L'avviso di capienza spiega già perché il servizio rifiuterebbe la richiesta.
    if (this.overCapacity()) {
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
        memberIds: this.members().map((member) => member.id),
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
          this.freeSlots.set(
            problem.code === ROOM_OCCUPIED_CODE ? this.bookableSlots(problem.freeSlots) : null,
          );
          // Un conflitto vuol dire che la disponibilità è cambiata: la linea del tempo la aggiorna.
          if (problem.status === 409) {
            this.availabilityRequests.next({ day: this.form.controls.day.value, keepCurrent: true });
          }
        },
      });
  }

  // Il campo «Giorno» è di sola lettura: Invio e Alt+Freccia giù aprono il calendario.
  // preventDefault evita che Invio invii anche il modulo.
  protected openCalendar(event: Event, picker: MatDatepicker<Date>): void {
    event.preventDefault();
    picker.open();
  }

  protected onMemberInput(text: string): void {
    this.memberSearchText.set(text);
    this.memberSearchTerms.next(text);
  }

  // Scelta di una proposta: diventa un chip, il testo si svuota e le proposte si chiudono.
  protected addMember(event: MatAutocompleteSelectedEvent, trigger: MatAutocompleteTrigger): void {
    const student = event.option.value as StudentSummary;
    if (!this.membersFull() && !this.members().some((member) => member.id === student.id)) {
      this.members.update((members) => [...members, student]);
    }
    this.clearMemberSearch();
    trigger.closePanel();
  }

  protected removeMember(student: StudentSummary): void {
    this.members.update((members) => members.filter((member) => member.id !== student.id));
  }

  // Invio senza una proposta evidenziata non aggiunge nulla e non invia il modulo: un membro si
  // aggiunge solo scegliendolo. Con una proposta evidenziata la sceglie l'autocomplete.
  protected onMemberEnter(event: Event, trigger: MatAutocompleteTrigger): void {
    if (!trigger.panelOpen || !trigger.activeOption) {
      event.preventDefault();
    }
  }

  private clearMemberSearch(): void {
    const input = this.memberInput()?.nativeElement;
    if (input) {
      input.value = '';
    }
    this.onMemberInput('');
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

  // Dopo la conferma: modulo vuoto per lo stesso spazio, senza messaggi di rifiuto né membri.
  protected bookAnother(): void {
    this.form.reset();
    this.members.set([]);
    this.memberSearchText.set('');
    this.clearRejection();
    this.confirmation.set(null);
    this.focusAfterRender(() => this.dayInput()?.nativeElement);
  }

  // Primo orario di inizio prenotabile dentro una fascia libera: oggi non sono prenotabili le mezz'ore
  // già passate, quindi una fascia che comincia prima dell'ora attuale parte dalla prima valida.
  private firstBookableStart(slot: FreeRange): string | null {
    const slotMinutes = (new Date(slot.end).getTime() - new Date(slot.start).getTime()) / 60_000;
    const from = toMinutes(campusTime(new Date(slot.start)));
    return (
      this.startOptions().find((time) => {
        const minutes = toMinutes(time);
        return minutes >= from && minutes < from + slotMinutes;
      }) ?? null
    );
  }

  // Delle fasce proposte si tengono quelle in cui si può davvero iniziare, tagliate al primo inizio
  // prenotabile: così ogni pulsante mostrato funziona e dice l'orario da cui si parte.
  private bookableSlots(slots: FreeRange[] | null): FreeRange[] | null {
    const day = this.selectedDay();
    if (slots === null || day === null) {
      return slots;
    }
    return slots.flatMap((slot) => {
      const start = this.firstBookableStart(slot);
      return start === null ? [] : [{ start: campusDateTime(day, start), end: slot.end }];
    });
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
  return {
    roomName: reservation.roomName,
    time: reservationTimeLabel(reservation),
    participants: reservation.participants,
  };
}

// 409, 422 e 503 portano il motivo: il codice dà il messaggio in italiano con i suoi limiti e, senza
// codice, vale il detail del servizio. Il 401 porta il detail quando presente, ma la sessione viene
// comunque chiusa dall'interceptor. Ogni altro errore riceve il messaggio generico.
function rejectionMessage(problem: ApiProblem): string | null {
  if (problem.status === 409 || problem.status === 422 || problem.status === 503) {
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
