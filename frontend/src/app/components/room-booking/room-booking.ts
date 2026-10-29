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
import { Subject, catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
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
import {
  MAX_MEMBERS,
  ReservationResponse,
  addDays,
  campusDateTime,
  campusTime,
  campusToday,
  halfHourSlots,
  pad,
  groupRejectionMessage,
  reservationTimeLabel,
} from '../../models/booking.model';
import { OpeningHours, RoomDetail, WEEK_DAYS } from '../../models/room.model';
import { StudentSummary } from '../../models/user.model';
import { AuthService } from '../../services/auth.service';
import { BookingsService } from '../../services/bookings.service';
import { RoomsService } from '../../services/rooms.service';
import { StudentsService } from '../../services/students.service';

// Giorni prenotabili oltre oggi (assunzione A2).
const MAX_DAYS_AHEAD = 14;

// Lunghezza massima del motivo, come in Bookings.
export const MAX_PURPOSE_LENGTH = 200;

// Messaggio di Bookings per il conflitto sullo spazio: solo con questo si propone la disponibilità.
// Limite noto: il servizio non espone un codice di errore, quindi il confronto è sul testo.
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
  protected readonly confirmation = signal<Confirmation | null>(null);
  protected readonly showAvailabilityLink = computed(
    () => this.errorMessage() === ROOM_CONFLICT_DETAIL,
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

  constructor() {
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
    this.errorMessage.set(null);

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
          this.errorMessage.set(rejectionMessage(error));
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

  // Dopo la conferma: modulo vuoto per lo stesso spazio, senza messaggi di rifiuto né membri.
  protected bookAnother(): void {
    this.form.reset();
    this.members.set([]);
    this.memberSearchText.set('');
    this.errorMessage.set(null);
    this.confirmation.set(null);
    this.focusAfterRender(() => this.dayInput()?.nativeElement);
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

// 409, 422 e 503 portano il motivo: per i rifiuti dei gruppi il messaggio si sceglie dal codice
// ("code"), per gli altri si usa il detail. Il 401 porta il detail quando presente, ma la sessione
// viene comunque chiusa dall'interceptor. Ogni altro errore riceve il messaggio generico.
function rejectionMessage(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) {
    return GENERIC_ERROR;
  }
  const detail: unknown = error.error?.detail;
  const hasDetail = typeof detail === 'string' && detail.trim() !== '';
  if (error.status === 409 || error.status === 422 || error.status === 503) {
    const byCode = groupRejectionMessage(error.error?.code, error.error?.limits);
    return byCode ?? (hasDetail ? detail : GENERIC_ERROR);
  }
  if (error.status === 401) {
    return hasDetail ? detail : null;
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
