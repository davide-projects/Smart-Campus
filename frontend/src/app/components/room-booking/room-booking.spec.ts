import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader, TestKey } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatAutocompleteHarness } from '@angular/material/autocomplete/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatChipGridHarness } from '@angular/material/chips/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import {
  MatDatepickerInputHarness,
  MatDatepickerToggleHarness,
} from '@angular/material/datepicker/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import { DailyAvailability } from '../../models/availability.model';
import { ReservationResponse } from '../../models/booking.model';
import { RoomDetail } from '../../models/room.model';
import { StudentSummary } from '../../models/user.model';
import { authInterceptor } from '../../services/auth.interceptor';
import { AuthService } from '../../services/auth.service';
import { RoomsService } from '../../services/rooms.service';
import { RoomBooking } from './room-booking';

@Component({ template: '' })
class EmptyPage {}

// Dati sintetici di prova. Novembre 2026: il 5 è giovedì, l'1, l'8 e il 15 sono domeniche.
const ROOM: RoomDetail = {
  id: 'r1',
  name: 'Aula A1',
  type: 'Classroom',
  buildingId: 'b1',
  buildingName: 'Edificio A',
  buildingCode: 'A',
  floor: 0,
  capacity: 30,
  isBookable: true,
  equipment: [],
  openingHours: [
    { dayOfWeek: 'Monday', opensAt: '08:00:00', closesAt: '20:00:00', isClosed: false },
    { dayOfWeek: 'Tuesday', opensAt: '08:00:00', closesAt: '20:00:00', isClosed: false },
    { dayOfWeek: 'Wednesday', opensAt: '08:00:00', closesAt: '20:00:00', isClosed: false },
    { dayOfWeek: 'Thursday', opensAt: '08:00:00', closesAt: '20:00:00', isClosed: false },
    { dayOfWeek: 'Friday', opensAt: '09:00:00', closesAt: '13:00:00', isClosed: false },
    { dayOfWeek: 'Saturday', opensAt: null, closesAt: null, isClosed: true },
    // Domenica senza orario: chiuso.
  ],
};

// 09:00 di giovedì 5 novembre 2026 a Roma.
const NOW = new Date('2026-11-05T08:00:00Z');

const ANNA: StudentSummary = { id: 's2', username: 'student2', displayName: 'Anna Bianchi' };
const LUCA: StudentSummary = { id: 's3', username: 'student3', displayName: 'Luca Verdi' };

// Studenti sintetici numerati, per riempire il gruppo.
function students(count: number): StudentSummary[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `m${i + 1}`,
    username: `studente${i + 1}`,
    displayName: `Studente Prova ${i + 1}`,
  }));
}

const ROOM_CONFLICT = 'Lo spazio è già prenotato in questa fascia.';

// Richiesta della linea del tempo: la pagina la fa a ogni giorno scelto.
const isAvailabilityRequest = (request: { url: string }): boolean =>
  request.url === '/api/bookings/availability';

// Disponibilità sintetica del 5 novembre dalle 08:00, in fasce da 30 minuti (true = libera).
function availabilityOf(states: boolean[], isOpen = true): DailyAvailability {
  const time = (index: number): string => {
    const hours = String(8 + Math.floor(index / 2)).padStart(2, '0');
    return `2026-11-05T${hours}:${index % 2 === 0 ? '00' : '30'}:00+01:00`;
  };
  return {
    roomId: 'r1',
    date: '2026-11-05',
    isOpen,
    slots: states.map((isFree, index) => ({ start: time(index), end: time(index + 1), isFree })),
  };
}
const GENERIC_ERROR = 'Non è stato possibile completare la prenotazione. Riprova più tardi.';

function created(overrides: Partial<ReservationResponse> = {}): ReservationResponse {
  return {
    id: 'p1',
    roomId: 'r1',
    roomName: 'Aula A1',
    start: '2026-11-05T10:00:00+01:00',
    end: '2026-11-05T11:00:00+01:00',
    participants: 1,
    status: 'Confirmed',
    ...overrides,
  };
}

function problem(
  status: number,
  title: string,
  detail: string,
  extra: object = {},
): [object, object] {
  return [
    { status, title, detail, ...extra },
    { status, statusText: title },
  ];
}

// Le date sono controllate; setInterval anche, perché lo usa il debounce di RxJS.
const FAKE_TIMERS: Parameters<typeof vi.useFakeTimers>[0] = {
  toFake: ['Date', 'setInterval', 'clearInterval'],
};
const DEBOUNCE_MS = 300;

// Variabile d'ambiente del fuso del processo di test, per simulare un dispositivo in un altro fuso.
const env = (globalThis as unknown as { process: { env: Record<string, string | undefined> } })
  .process.env;

describe('RoomBooking', () => {
  const originalTimeZone = env['TZ'];
  let harness: RouterTestingHarness;
  let loader: HarnessLoader;
  let http: HttpTestingController;
  let getRoomById: ReturnType<typeof vi.fn<(id: string) => Observable<RoomDetail>>>;

  async function render(url = '/rooms/r1/book'): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'rooms/:id/book', component: RoomBooking },
          { path: 'rooms/:id', component: EmptyPage },
          { path: 'rooms', component: EmptyPage },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RoomsService, useValue: { getRoomById } },
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create(url);
    loader = TestbedHarnessEnvironment.loader(harness.fixture);
    await harness.fixture.whenStable();
  }

  function element(): HTMLElement {
    return harness.routeNativeElement!;
  }

  function alertText(): string | null {
    return element().querySelector('[role="alert"] p')?.textContent!.trim() ?? null;
  }

  async function stable(): Promise<void> {
    await harness.fixture.whenStable();
  }

  async function dayInput(): Promise<MatDatepickerInputHarness> {
    return loader.getHarness(MatDatepickerInputHarness);
  }

  async function select(label: 'Ora di inizio' | 'Ora di fine'): Promise<MatSelectHarness> {
    const field = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
    return (await field.getControl(MatSelectHarness))!;
  }

  // Il campo «Giorno» è di sola lettura: il calendario si apre dal pulsante accanto.
  async function openCalendar(): Promise<void> {
    await (await loader.getHarness(MatDatepickerToggleHarness)).openCalendar();
  }

  async function chooseDay(day: number): Promise<void> {
    const input = await dayInput();
    await openCalendar();
    await (await input.getCalendar()).selectCell({ text: String(day) });
    await stable();
  }

  async function chooseTime(label: 'Ora di inizio' | 'Ora di fine', time: string): Promise<void> {
    const control = await select(label);
    await control.open();
    await control.clickOptions({ text: time });
    await stable();
  }

  async function optionTexts(label: 'Ora di inizio' | 'Ora di fine'): Promise<string[]> {
    const control = await select(label);
    await control.open();
    const texts = await Promise.all((await control.getOptions()).map((o) => o.getText()));
    await control.close();
    return texts;
  }

  async function confirmButton(): Promise<MatButtonHarness> {
    return loader.getHarness(MatButtonHarness.with({ text: /Conferma prenotazione/ }));
  }

  async function fillAndSubmit(day = 5, start = '10:00', end = '11:00'): Promise<void> {
    await chooseDay(day);
    await chooseTime('Ora di inizio', start);
    await chooseTime('Ora di fine', end);
    await (await confirmButton()).click();
    await stable();
  }

  async function respond(body: object | null, options: object): Promise<void> {
    http.expectOne('/api/bookings').flush(body, options);
    await stable();
  }

  // Ricerca degli studenti nel campo «Membri del gruppo», con l'attesa del debounce.
  async function memberSearch(): Promise<MatAutocompleteHarness> {
    return loader.getHarness(MatAutocompleteHarness);
  }

  // Scrive nel campo al posto del testo precedente e attende la pausa del debounce.
  async function typeMember(text: string): Promise<void> {
    const search = await memberSearch();
    await search.clear();
    await search.enterText(text);
    vi.advanceTimersByTime(DEBOUNCE_MS);
    await stable();
  }

  async function respondSearch(text: string, found: StudentSummary[]): Promise<void> {
    const request = http.expectOne((req) => req.url === '/api/identity/students');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe(text);
    request.flush(found);
    await settle();
  }

  // L'autocomplete aggiorna il pannello con un ritardo di RxJS (setInterval, controllato).
  async function settle(): Promise<void> {
    await stable();
    vi.advanceTimersByTime(0);
    await stable();
  }

  async function addMember(student: StudentSummary, text = student.displayName): Promise<void> {
    await typeMember(text);
    await respondSearch(text, [student]);
    await (await memberSearch()).selectOption({ text: new RegExp(student.displayName) });
    // Lo studente fa una pausa prima di scrivere ancora: passa anche il campo svuotato.
    vi.advanceTimersByTime(DEBOUNCE_MS);
    await stable();
  }

  async function optionTextsOfSearch(): Promise<string[]> {
    const options = await (await memberSearch()).getOptions();
    return Promise.all(options.map((option) => option.getText()));
  }

  async function chipNames(): Promise<string[]> {
    const rows = await (await loader.getHarness(MatChipGridHarness)).getRows();
    return Promise.all(rows.map((row) => row.getText()));
  }

  async function removeChip(name: string): Promise<void> {
    const remove = element().querySelector<HTMLButtonElement>(
      `button[aria-label="Rimuovi ${name}"]`,
    )!;
    remove.click();
    await stable();
  }

  function participantsText(): string {
    return element().querySelector('.participants')!.textContent!.trim();
  }

  function capacityWarning(): string | null {
    return element().querySelector('.capacity-warning')?.textContent!.trim() ?? null;
  }

  beforeEach(() => {
    vi.useFakeTimers(FAKE_TIMERS);
    vi.setSystemTime(NOW);
    getRoomById = vi.fn(() => of(ROOM));
  });

  afterEach(() => {
    // Le richieste della linea del tempo non interessano i test che non le verificano: si scartano.
    http?.match(isAvailabilityRequest);
    http?.verify();
    vi.useRealTimers();
    if (originalTimeZone === undefined) {
      delete env['TZ'];
    } else {
      env['TZ'] = originalTimeZone;
    }
  });

  describe('room', () => {
    it('shows the name of the room and the booking form', async () => {
      await render();

      expect(getRoomById).toHaveBeenCalledWith('r1');
      expect(element().querySelector('h1')!.textContent).toContain('Prenota spazio');
      expect(element().querySelector('.room-name')!.textContent!.trim()).toBe('Aula A1');
      expect(element().querySelector('form')).not.toBeNull();
    });

    it('shows «Spazio non trovato» without the form when Rooms answers 404', async () => {
      getRoomById.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
      );
      await render();

      expect(element().textContent).toContain('Spazio non trovato');
      expect(element().querySelector('form')).toBeNull();
    });

    it('pre-compila giorno, ora di inizio e fine dai parametri di query', async () => {
      await render('/rooms/r1/book?date=2026-11-05&start=10:00&end=11:30');

      const day = await dayInput();
      expect(await day.getValue()).toBe('05/11/2026');
      expect(await (await select('Ora di inizio')).getValueText()).toBe('10:00');
      expect(await (await select('Ora di fine')).getValueText()).toBe('11:30');
    });

    it('ignores start and end times from query parameters when they are not valid for the day', async () => {
      await render('/rooms/r1/book?date=2026-11-05&start=07:00&end=07:30');

      const day = await dayInput();
      expect(await day.getValue()).toBe('05/11/2026');
      expect(await (await select('Ora di inizio')).getValueText()).toBe('');
      expect(await (await select('Ora di fine')).getValueText()).toBe('');
    });
  });

  describe('days and times', () => {
    it('offers only the days from today to today + 14, excluding closing days', async () => {
      await render();
      const input = await dayInput();

      await openCalendar();
      const calendar = await input.getCalendar();
      const enabled = await Promise.all(
        (await calendar.getCells({ disabled: false })).map((cell) => cell.getText()),
      );
      // Prima del 5 e dopo il 19 fuori intervallo; sabato 7 e 14 chiusi, domenica 8 e 15 senza orario.
      expect(enabled).toEqual(['5', '6', '9', '10', '11', '12', '13', '16', '17', '18', '19']);
    });

    it('offers only half hours inside the opening hours of the chosen day', async () => {
      await render();
      // Giovedì 12: un giorno futuro, l'inizio parte dall'apertura.
      await chooseDay(12);

      const starts = await optionTexts('Ora di inizio');
      const ends = await optionTexts('Ora di fine');

      expect(starts[0]).toBe('08:00');
      expect(starts.at(-1)).toBe('19:30');
      expect(starts).toHaveLength(24);
      expect(ends[0]).toBe('08:30');
      expect(ends.at(-1)).toBe('20:00');
      expect(ends).toHaveLength(24);
      for (const time of [...starts, ...ends]) {
        expect(time).toMatch(/^\d{2}:(00|30)$/);
      }
      expect(element().textContent).toContain('Aperto dalle 08:00 alle 20:00');
    });

    it('offers today only the half hours after the campus time', async () => {
      // 09:10 di giovedì 5 novembre 2026 a Roma.
      vi.setSystemTime(new Date('2026-11-05T08:10:00Z'));
      await render();
      await chooseDay(5);

      const starts = await optionTexts('Ora di inizio');

      expect(starts[0]).toBe('09:30');
      expect(starts.at(-1)).toBe('19:30');
      expect(starts).toHaveLength(21);
    });

    it('does not offer today the half hour that is the current campus time', async () => {
      // NOW: 09:00 di Roma.
      await render();
      await chooseDay(5);

      expect((await optionTexts('Ora di inizio'))[0]).toBe('09:30');
    });

    it('offers today the half hours after the campus time when the device is in UTC', async () => {
      env['TZ'] = 'UTC';
      vi.setSystemTime(new Date('2026-11-05T08:10:00Z'));
      await render();
      await chooseDay(5);

      expect((await optionTexts('Ora di inizio'))[0]).toBe('09:30');
    });

    it('clears the times that are outside the opening hours of the new day', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');
      await chooseTime('Ora di fine', '19:00');

      // Venerdì 6 è aperto dalle 09:00 alle 13:00: le 10:00 restano, le 19:00 no.
      await chooseDay(6);

      expect(await (await select('Ora di inizio')).getValueText()).toBe('10:00');
      expect(await (await select('Ora di fine')).getValueText()).toBe('');
      expect(await optionTexts('Ora di fine')).toEqual([
        '10:30',
        '11:00',
        '11:30',
        '12:00',
        '12:30',
        '13:00',
      ]);
    });

    it('offers only end times after the chosen start', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');

      const ends = await optionTexts('Ora di fine');

      expect(ends[0]).toBe('10:30');
      expect(ends.at(-1)).toBe('20:00');
      expect(ends).toHaveLength(20);
    });

    it('clears the end and marks it required when the new start passes it', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');
      await chooseTime('Ora di fine', '11:00');

      await chooseTime('Ora di inizio', '11:00');

      expect(await (await select('Ora di fine')).getValueText()).toBe('');
      const field = await loader.getHarness(
        MatFormFieldHarness.with({ floatingLabelText: 'Ora di fine' }),
      );
      expect(await field.getTextErrors()).toEqual(["L'ora di fine è obbligatoria."]);
    });

    it('keeps the end when it is still after the new start', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');
      await chooseTime('Ora di fine', '12:00');

      await chooseTime('Ora di inizio', '11:00');

      expect(await (await select('Ora di fine')).getValueText()).toBe('12:00');
      const field = await loader.getHarness(
        MatFormFieldHarness.with({ floatingLabelText: 'Ora di fine' }),
      );
      expect(await field.getTextErrors()).toEqual([]);
    });

    it('fills the day only from the calendar', async () => {
      await render();
      const input = await dayInput();
      const host = await input.host();

      expect(await host.getProperty<boolean>('readOnly')).toBe(true);

      // Il browser non accetta testo in un campo di sola lettura (jsdom non lo simula: qui si
      // verifica l'attributo). Il giorno arriva dal calendario.
      expect(await host.getAttribute('readonly')).not.toBeNull();
      expect(await input.getValue()).toBe('');

      await chooseDay(5);
      expect(await input.getValue()).not.toBe('');
      expect(await optionTexts('Ora di inizio')).toContain('10:00');
    });

    it('keeps the calendar toggle reachable from the keyboard', async () => {
      await render();
      const toggle = element().querySelector<HTMLButtonElement>('mat-datepicker-toggle button')!;

      expect(toggle.tabIndex).not.toBe(-1);
      expect(toggle.getAttribute('aria-label')).toBeTruthy();
    });

    it('opens the calendar with Enter on the day field, without sending the form', async () => {
      await render();
      const input = await dayInput();
      // Evento costruito a mano per leggere defaultPrevented: nel browser è ciò che impedisce
      // a Invio di inviare anche il modulo (jsdom non simula l'invio implicito).
      const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });

      element().querySelector('input[formControlName="day"]')!.dispatchEvent(enter);
      await stable();

      expect(await input.isCalendarOpen()).toBe(true);
      expect(enter.defaultPrevented).toBe(true);
      http.expectNone('/api/bookings');
      expect(await (await select('Ora di inizio')).isDisabled()).toBe(false);
      expect(element().querySelector('mat-error')).toBeNull();
    });

    it('opens the calendar with Alt+Down on the day field', async () => {
      await render();
      const input = await dayInput();

      // Il campo è di sola lettura, quindi Material ignora Alt+Freccia giù: lo gestisce la pagina.
      await (await input.host()).sendKeys({ alt: true }, TestKey.DOWN_ARROW);
      await stable();

      expect(await input.isCalendarOpen()).toBe(true);
    });

    it('still sends a single request when the complete form is submitted from another field', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');
      await chooseTime('Ora di fine', '11:00');

      // Invio implicito del modulo (quello che il browser fa con Invio fuori dal campo «Giorno»).
      element()
        .querySelector('form')!
        .dispatchEvent(new Event('submit', { cancelable: true }));
      await stable();

      expect(await (await dayInput()).isCalendarOpen()).toBe(false);
      await respond(created(), { status: 201, statusText: 'Created' });
    });
  });

  describe('availability of the day', () => {
    function availabilityRequest(date: string) {
      return http.expectOne(
        (request) => isAvailabilityRequest(request) && request.params.get('date') === date,
      );
    }

    function srTexts(): string[] {
      return [...element().querySelectorAll('app-availability-timeline .block .sr-only')].map(
        (node) => node.textContent!.trim(),
      );
    }

    it('shows nothing before a day is chosen', async () => {
      await render();

      expect(element().querySelector('.day-availability')).toBeNull();
      http.expectNone(isAvailabilityRequest);
    });

    it('asks for the availability of the chosen day and shows it as a timeline', async () => {
      await render();
      await chooseDay(5);

      const request = availabilityRequest('2026-11-05');
      expect(request.request.params.get('roomId')).toBe('r1');
      expect(element().querySelector('.day-availability mat-spinner')).not.toBeNull();

      request.flush(availabilityOf([true, true, false, false]));
      await stable();

      expect(element().querySelector('.day-availability mat-spinner')).toBeNull();
      expect(srTexts()).toEqual(['Libera dalle 08:00 alle 09:00', 'Occupata dalle 09:00 alle 10:00']);
      // La linea del tempo sta tra il campo «Giorno» e gli orari.
      const section = element().querySelector('.day-availability')!;
      expect(section.previousElementSibling!.tagName).toBe('MAT-FORM-FIELD');
      expect(section.nextElementSibling!.classList).toContain('times');
    });

    it('asks for the availability of the day taken from the query parameters', async () => {
      await render('/rooms/r1/book?date=2026-11-05&start=10:00&end=11:00');

      availabilityRequest('2026-11-05').flush(availabilityOf([true, true]));
      await stable();

      expect(srTexts()).toEqual(['Libera dalle 08:00 alle 09:00']);
    });

    it('cancels the earlier request when another day is chosen', async () => {
      await render();
      await chooseDay(5);
      await chooseDay(6);

      expect(availabilityRequest('2026-11-05').cancelled).toBe(true);
      const second = availabilityRequest('2026-11-06');
      expect(second.cancelled).toBe(false);

      second.flush(availabilityOf([false, false]));
      await stable();

      expect(srTexts()).toEqual(['Occupata dalle 08:00 alle 09:00']);
    });

    it('says that the space is closed when the service reports no opening', async () => {
      await render();
      await chooseDay(5);

      availabilityRequest('2026-11-05').flush(availabilityOf([], false));
      await stable();

      expect(element().querySelector('app-availability-timeline')).toBeNull();
      expect(element().querySelector('.day-availability')!.textContent).toContain(
        'Lo spazio non è aperto nel giorno scelto.',
      );
    });

    it('lets the student book when the availability does not load', async () => {
      await render();
      await chooseDay(5);

      availabilityRequest('2026-11-05').flush(null, { status: 500, statusText: 'Server Error' });
      await stable();

      expect(element().querySelector('app-availability-timeline')).toBeNull();
      expect(element().querySelector('.day-availability')!.textContent).toContain(
        'Non è stato possibile caricare le fasce orarie',
      );
      expect(element().querySelector('[role="alert"]')).toBeNull();
      expect(await (await confirmButton()).isDisabled()).toBe(false);
    });
  });

  describe('form', () => {
    it('marks the missing required fields and sends no request', async () => {
      await render();

      await (await confirmButton()).click();
      await stable();

      const errors = await Promise.all(
        (await loader.getAllHarnesses(MatFormFieldHarness)).map((f) => f.getTextErrors()),
      );
      expect(errors.flat()).toEqual([
        'Il giorno è obbligatorio.',
        "L'ora di inizio è obbligatoria.",
        "L'ora di fine è obbligatoria.",
      ]);
      http.expectNone('/api/bookings');
    });

    it('does not accept more than 200 characters in the purpose', async () => {
      await render();
      const purpose = await loader.getHarness(MatInputHarness.with({ selector: 'textarea' }));

      expect(await (await purpose.host()).getAttribute('maxlength')).toBe('200');

      // Anche un testo più lungo incollato da script non viene inviato.
      await purpose.setValue('x'.repeat(201));
      await fillAndSubmit();

      const field = await loader.getHarness(
        MatFormFieldHarness.with({ floatingLabelText: 'Motivo (facoltativo)' }),
      );
      expect(await field.getTextErrors()).toEqual([
        'Il motivo può avere al massimo 200 caratteri.',
      ]);
      http.expectNone('/api/bookings');
    });

    it('sends day and times in campus time when the device is in UTC', async () => {
      env['TZ'] = 'UTC';
      await render();
      const purpose = await loader.getHarness(MatInputHarness.with({ selector: 'textarea' }));
      await purpose.setValue('  Ripasso di analisi  ');

      await fillAndSubmit();

      const request = http.expectOne('/api/bookings');
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({
        roomId: 'r1',
        start: '2026-11-05T10:00:00+01:00',
        end: '2026-11-05T11:00:00+01:00',
        purpose: 'Ripasso di analisi',
        memberIds: [],
      });
      request.flush(created(), { status: 201, statusText: 'Created' });
    });

    it('sends purpose null when it is empty', async () => {
      await render();

      await fillAndSubmit();

      const request = http.expectOne('/api/bookings');
      expect(request.request.body.purpose).toBeNull();
      request.flush(created(), { status: 201, statusText: 'Created' });
    });

    it('sends a single request when the confirmation is activated twice', async () => {
      await render();
      await fillAndSubmit();

      const button = await confirmButton();
      expect(await button.isDisabled()).toBe(true);
      await button.click();
      // Anche un secondo invio del modulo (es. Invio) non parte durante la richiesta.
      element().querySelector('form')!.dispatchEvent(new Event('submit'));
      await stable();

      await respond(created(), { status: 201, statusText: 'Created' });
    });
  });

  describe('confirmation', () => {
    async function confirm(): Promise<void> {
      await render();
      await fillAndSubmit();
      await respond(created(), { status: 201, statusText: 'Created' });
    }

    it('replaces the form with room, day and time of the reservation', async () => {
      await confirm();

      const confirmation = element().querySelector('.confirmation')!;
      expect(confirmation.getAttribute('role')).toBe('status');
      expect(confirmation.querySelector('.confirmation-room')!.textContent!.trim()).toBe('Aula A1');
      expect(confirmation.querySelector('.confirmation-time')!.textContent!.trim()).toBe(
        'giovedì 5 novembre 2026, 10:00–11:00',
      );
      expect(element().querySelector('form')).toBeNull();
      expect(document.activeElement?.textContent).toContain('Prenotazione confermata');
    });

    it('shows the campus time even when the device is in UTC', async () => {
      env['TZ'] = 'UTC';
      await render();
      await fillAndSubmit();
      await respond(
        created({ start: '2026-11-05T09:00:00+00:00', end: '2026-11-05T10:00:00+00:00' }),
        { status: 201, statusText: 'Created' },
      );

      expect(element().querySelector('.confirmation-time')!.textContent!.trim()).toBe(
        'giovedì 5 novembre 2026, 10:00–11:00',
      );
    });

    it('goes back to the room with «Torna allo spazio»', async () => {
      await confirm();

      await (await loader.getHarness(MatButtonHarness.with({ text: /Torna allo spazio/ }))).click();
      await stable();

      expect(TestBed.inject(Router).url).toBe('/rooms/r1');
    });

    it("offers an empty form for the same room with «Prenota un'altra fascia»", async () => {
      await confirm();

      await (
        await loader.getHarness(MatButtonHarness.with({ text: /Prenota un'altra fascia/ }))
      ).click();
      await stable();

      expect(element().querySelector('.confirmation')).toBeNull();
      expect(element().querySelector('.day-availability')).toBeNull();
      expect(element().querySelector('.room-name')!.textContent!.trim()).toBe('Aula A1');
      expect(await (await dayInput()).getValue()).toBe('');
      expect(await (await select('Ora di inizio')).getValueText()).toBe('');
      expect(await (await select('Ora di fine')).getValueText()).toBe('');
      expect(element().querySelector('[role="alert"]')).toBeNull();
      expect(await (await confirmButton()).isDisabled()).toBe(false);
    });
  });

  describe('rejections', () => {
    async function availabilityLink(): Promise<HTMLAnchorElement | null> {
      return element().querySelector<HTMLAnchorElement>('[role="alert"] a');
    }

    it.each([
      'Lo spazio è aperto dalle 08:00 alle 20:00.',
      'Una prenotazione può durare al massimo 4 ore.',
      'Si può prenotare al massimo 14 giorni prima.',
      'Non è possibile prenotare nel passato.',
    ])('shows the 422 detail «%s» and keeps the values', async (detail) => {
      await render();
      await fillAndSubmit(5, '19:00', '20:00');

      await respond(...problem(422, 'Prenotazione non valida', detail));

      expect(alertText()).toBe(detail);
      expect(await availabilityLink()).toBeNull();
      expect(await (await dayInput()).getValue()).not.toBe('');
      expect(await (await select('Ora di inizio')).getValueText()).toBe('19:00');
      expect(await (await select('Ora di fine')).getValueText()).toBe('20:00');
      expect(await (await confirmButton()).isDisabled()).toBe(false);
    });

    it('shows the room conflict without a link to the room: the timeline is already in the page', async () => {
      await render();
      await fillAndSubmit();

      await respond(...problem(409, 'Spazio già prenotato', ROOM_CONFLICT));

      expect(alertText()).toBe(ROOM_CONFLICT);
      expect(await availabilityLink()).toBeNull();
      expect(element().querySelector('[role="alert"] a')).toBeNull();
    });

    it('writes the message of the reason code and its limits, not the text of the service', async () => {
      await render();
      await fillAndSubmit(5, '19:00', '20:00');

      await respond(
        ...problem(422, 'Fuori orario', 'Testo del servizio.', {
          code: 'outside-opening-hours',
          limits: { opensAt: '09:00', closesAt: '13:00' },
        }),
      );

      expect(alertText()).toBe('Lo spazio è aperto dalle 09:00 alle 13:00.');
    });

    it('recognises a room conflict by its code', async () => {
      await render();
      await fillAndSubmit();

      await respond(
        ...problem(409, 'Spazio già prenotato', 'Testo del servizio.', {
          code: 'room-occupied',
          freeSlots: [],
        }),
      );

      expect(alertText()).toBe(ROOM_CONFLICT);
      expect(await availabilityLink()).toBeNull();
    });

    it('does not link to the availability for a conflict on the person', async () => {
      await render();
      await fillAndSubmit();

      await respond(
        ...problem(409, 'Hai già una prenotazione', 'Testo del servizio.', {
          code: 'person-occupied',
        }),
      );

      expect(alertText()).toBe('Hai già una prenotazione in questa fascia oraria.');
      expect(await availabilityLink()).toBeNull();
    });

    it('keeps the values of the form after a rejection with a code', async () => {
      await render();
      await fillAndSubmit(5, '19:00', '20:00');

      await respond(
        ...problem(422, 'Durata eccessiva', 'Testo del servizio.', {
          code: 'duration-exceeded',
          limits: { maxDurationMinutes: 240 },
        }),
      );

      expect(alertText()).toBe('Una prenotazione può durare al massimo 4 ore.');
      expect(await (await dayInput()).getValue()).not.toBe('');
      expect(await (await select('Ora di inizio')).getValueText()).toBe('19:00');
      expect(await (await select('Ora di fine')).getValueText()).toBe('20:00');
    });

    it.each([
      'Hai già una prenotazione in questa fascia oraria.',
      "Hai un'altra prenotazione in corso, riprova tra qualche secondo.",
    ])('shows the 409 detail «%s» without the availability link', async (detail) => {
      await render();
      await fillAndSubmit();

      await respond(...problem(409, 'Conflitto', detail));

      expect(alertText()).toBe(detail);
      expect(await availabilityLink()).toBeNull();
    });

    it('updates the timeline after a room conflict, without emptying it while it loads', async () => {
      await render();
      await fillAndSubmit();
      const first = http.expectOne(isAvailabilityRequest);
      first.flush(availabilityOf([true, true, true, true]));
      await stable();
      expect(element().querySelectorAll('app-availability-timeline .block')).toHaveLength(1);

      await respond(
        ...problem(409, 'Spazio già prenotato', ROOM_CONFLICT, {
          code: 'room-occupied',
          freeSlots: [],
        }),
      );

      const refresh = http.expectOne(
        (request) => isAvailabilityRequest(request) && request.params.get('date') === '2026-11-05',
      );
      expect(element().querySelector('app-availability-timeline')).not.toBeNull();
      expect(element().querySelector('.day-availability mat-spinner')).toBeNull();

      refresh.flush(availabilityOf([true, true, false, false]));
      await stable();

      expect(
        [...element().querySelectorAll('app-availability-timeline .block .sr-only')].map((node) =>
          node.textContent!.trim(),
        ),
      ).toEqual(['Libera dalle 08:00 alle 09:00', 'Occupata dalle 09:00 alle 10:00']);
    });

    it.each(['person-occupied', 'members-occupied', 'member-booking-in-progress'])(
      'does not update the timeline after a 409 «%s», which does not change the room',
      async (code) => {
        await render();
        await fillAndSubmit();

        await respond(...problem(409, 'Conflitto', 'Testo del servizio.', { code }));

        // Resta solo la richiesta fatta scegliendo il giorno.
        expect(http.match(isAvailabilityRequest)).toHaveLength(1);
      },
    );

    it('does not update the timeline after a 409 without a code', async () => {
      await render();
      await fillAndSubmit();

      await respond(...problem(409, 'Conflitto', ROOM_CONFLICT));

      expect(http.match(isAvailabilityRequest)).toHaveLength(1);
    });

    it('does not ask for the availability again after a rejection that is not a conflict', async () => {
      await render();
      await fillAndSubmit(5, '19:00', '20:00');

      await respond(...problem(422, 'Prenotazione non valida', 'Non è possibile prenotare nel passato.'));

      // Resta solo la richiesta fatta scegliendo il giorno.
      expect(http.match(isAvailabilityRequest)).toHaveLength(1);
    });

    describe('free slots', () => {
      // Lunedì 9 novembre 2026, 08:00-20:00: un giorno futuro, così nessuna fascia è già iniziata.
      const FREE_SLOTS = [
        { start: '2026-11-09T08:00:00+01:00', end: '2026-11-09T10:00:00+01:00' },
        { start: '2026-11-09T11:00:00+01:00', end: '2026-11-09T20:00:00+01:00' },
      ];

      async function conflict(freeSlots?: object[]): Promise<void> {
        await respond(
          ...problem(409, 'Spazio già prenotato', ROOM_CONFLICT, {
            code: 'room-occupied',
            ...(freeSlots === undefined ? {} : { freeSlots }),
          }),
        );
      }

      async function slotButtons(): Promise<MatButtonHarness[]> {
        return loader.getAllHarnesses(MatButtonHarness.with({ text: /\d{2}:\d{2}–\d{2}:\d{2}/ }));
      }

      it('offers the free slots of the day after a room conflict', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');

        await conflict(FREE_SLOTS);

        const texts = await Promise.all((await slotButtons()).map((button) => button.getText()));
        expect(texts).toHaveLength(2);
        expect(texts[0]).toContain('08:00–10:00');
        expect(texts[1]).toContain('11:00–20:00');
      });

      it.each<[number, string, string]>([
        [0, '08:00', '09:00'],
        [1, '11:00', '12:00'],
      ])('chooses slot %i and keeps the requested hour', async (index, start, end) => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');
        await conflict(FREE_SLOTS);

        await (await slotButtons())[index].click();
        await stable();

        expect(await (await select('Ora di inizio')).getValueText()).toBe(start);
        expect(await (await select('Ora di fine')).getValueText()).toBe(end);
      });

      it('takes the whole slot when it is shorter than the requested duration', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '12:00');
        await conflict([{ start: '2026-11-09T08:00:00+01:00', end: '2026-11-09T09:00:00+01:00' }]);

        await (await slotButtons())[0].click();
        await stable();

        expect(await (await select('Ora di inizio')).getValueText()).toBe('08:00');
        expect(await (await select('Ora di fine')).getValueText()).toBe('09:00');
      });

      it('clears the rejection, focuses the confirmation and sends the new times', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');
        await conflict(FREE_SLOTS);

        await (await slotButtons())[0].click();
        await stable();

        expect(alertText()).toBeNull();
        expect(await slotButtons()).toHaveLength(0);
        expect(await (await confirmButton()).isFocused()).toBe(true);

        await (await confirmButton()).click();
        await stable();
        const request = http.expectOne('/api/bookings');
        expect(request.request.body).toEqual({
          roomId: 'r1',
          start: '2026-11-09T08:00:00+01:00',
          end: '2026-11-09T09:00:00+01:00',
          purpose: null,
          memberIds: [],
        });
        request.flush(created(), { status: 201, statusText: 'Created' });
      });

      it('starts from the first half hour still bookable when a slot began in the past', async () => {
        await render();
        // Oggi, giovedì 5 novembre, sono le 09:00 a Roma: la fascia 08:00-12:00 è già iniziata.
        await fillAndSubmit(5, '10:00', '11:00');

        await conflict([{ start: '2026-11-05T08:00:00+01:00', end: '2026-11-05T12:00:00+01:00' }]);

        const buttons = await slotButtons();
        expect(buttons).toHaveLength(1);
        expect(await buttons[0].getText()).toContain('09:30–12:00');

        await buttons[0].click();
        await stable();

        expect(await (await select('Ora di inizio')).getValueText()).toBe('09:30');
        expect(await (await select('Ora di fine')).getValueText()).toBe('10:30');
      });

      it('does not offer a slot that is already over and says there are no free slots', async () => {
        await render();
        await fillAndSubmit(5, '10:00', '11:00');

        await conflict([{ start: '2026-11-05T08:00:00+01:00', end: '2026-11-05T09:00:00+01:00' }]);

        expect(await slotButtons()).toHaveLength(0);
        expect(element().querySelector('.free-slots')?.textContent).toContain(
          'Non ci sono fasce libere in questo giorno',
        );
      });

      it('says explicitly that there are no free slots when the list is empty', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');

        await conflict([]);

        expect(alertText()).toBe(ROOM_CONFLICT);
        expect(element().querySelector('.free-slots')?.textContent).toContain(
          'Non ci sono fasce libere in questo giorno',
        );
        expect(await slotButtons()).toHaveLength(0);
      });

      it('proposes nothing when the service sends no free slots', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');

        await conflict();

        expect(element().querySelector('.free-slots')).toBeNull();
      });

      it('proposes nothing for a rejection that is not a room conflict', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');

        await respond(
          ...problem(409, 'Hai già una prenotazione', 'Testo del servizio.', {
            code: 'person-occupied',
          }),
        );

        expect(element().querySelector('.free-slots')).toBeNull();
      });

      it('removes the proposals when the day changes', async () => {
        await render();
        await fillAndSubmit(9, '10:00', '11:00');
        await conflict(FREE_SLOTS);

        await chooseDay(10);

        expect(element().querySelector('.free-slots')).toBeNull();
      });
    });

    it('lets the student try again with the same form', async () => {
      await render();
      await fillAndSubmit();
      await respond(
        ...problem(
          409,
          'Prenotazione in corso',
          "Hai un'altra prenotazione in corso, riprova tra qualche secondo.",
        ),
      );

      await (await confirmButton()).click();
      await stable();

      await respond(created(), { status: 201, statusText: 'Created' });
      expect(element().querySelector('.confirmation')).not.toBeNull();
    });

    it('shows the generic message when the service fails', async () => {
      await render();
      await fillAndSubmit();

      await respond(null, { status: 500, statusText: 'Server Error' });

      expect(alertText()).toBe(GENERIC_ERROR);
    });

    it('shows the generic message when the service does not respond', async () => {
      await render();
      await fillAndSubmit();

      http.expectOne('/api/bookings').error(new ProgressEvent('error'));
      await stable();

      expect(alertText()).toBe(GENERIC_ERROR);
    });
  });

  describe('group members', () => {
    it('sends no search with a single character', async () => {
      await render();

      await typeMember('b');

      http.expectNone((req) => req.url === '/api/identity/students');
      expect(await (await memberSearch()).isOpen()).toBe(false);
    });

    it('searches once after the pause and offers name and username', async () => {
      await render();

      await (await memberSearch()).enterText('bia');
      // Prima della pausa non parte nulla.
      http.expectNone((req) => req.url === '/api/identity/students');
      vi.advanceTimersByTime(DEBOUNCE_MS);
      await stable();
      await respondSearch('bia', [ANNA]);

      const texts = await optionTextsOfSearch();
      expect(texts).toHaveLength(1);
      expect(texts[0]).toContain('Anna Bianchi');
      expect(texts[0]).toContain('student2');
    });

    it('adds the chosen student as a chip, empties the field and closes the panel', async () => {
      await render();

      await addMember(ANNA, 'bia');

      expect(await chipNames()).toEqual(['Anna Bianchi']);
      const search = await memberSearch();
      expect(await search.getValue()).toBe('');
      expect(await search.isOpen()).toBe(false);
    });

    it('removes a member with «Rimuovi <nome>»', async () => {
      await render();
      await addMember(ANNA, 'bia');
      await addMember(LUCA, 'ver');

      await removeChip('Anna Bianchi');

      expect(await chipNames()).toEqual(['Luca Verdi']);
      expect(participantsText()).toBe('Partecipanti: 2');
    });

    it('removes a member with the chip remove harness', async () => {
      await render();
      await addMember(ANNA, 'bia');

      const [row] = await (await loader.getHarness(MatChipGridHarness)).getRows();
      await row.remove();
      await stable();

      expect(await chipNames()).toEqual([]);
      expect(participantsText()).toBe('Partecipanti: 1');
    });

    it('does not offer again a student who is already a member', async () => {
      await render();
      await addMember(ANNA, 'bia');

      await typeMember('bia');
      await respondSearch('bia', [ANNA]);

      expect(await optionTextsOfSearch()).toEqual(['Nessuno studente trovato.']);
    });

    it('shows «Nessuno studente trovato.» when the search finds nobody', async () => {
      await render();

      await typeMember('Conti');
      await respondSearch('Conti', []);

      const options = await (await memberSearch()).getOptions();
      expect(options).toHaveLength(1);
      expect(await options[0].getText()).toBe('Nessuno studente trovato.');
      expect(await options[0].isDisabled()).toBe(true);
      await options[0].click();
      await stable();
      expect(await chipNames()).toEqual([]);
    });

    it('adds nothing and sends no booking with Enter on free text', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');
      await chooseTime('Ora di fine', '11:00');
      await (await memberSearch()).enterText('Elena Conti');
      const input = element().querySelector<HTMLInputElement>('.members-field input')!;
      // Evento costruito a mano per leggere defaultPrevented: nel browser è ciò che impedisce a
      // Invio di inviare il modulo (jsdom non simula l'invio implicito).
      const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });

      input.dispatchEvent(enter);
      await stable();

      expect(enter.defaultPrevented).toBe(true);
      expect(await chipNames()).toEqual([]);
      http.expectNone('/api/bookings');
      // La ricerca del testo scritto può partire, ma non aggiunge membri.
      vi.advanceTimersByTime(DEBOUNCE_MS);
      await stable();
      await respondSearch('Elena Conti', []);
      expect(await chipNames()).toEqual([]);
    });

    it('shows the search error with role alert and keeps chips and form values', async () => {
      await render();
      await chooseDay(5);
      await chooseTime('Ora di inizio', '10:00');
      await addMember(ANNA, 'bia');

      await typeMember('ver');
      http
        .expectOne((req) => req.url === '/api/identity/students')
        .flush(null, { status: 500, statusText: 'Server Error' });
      await stable();

      const error = element().querySelector('.member-search-error')!;
      expect(error.getAttribute('role')).toBe('alert');
      expect(error.textContent!.trim()).toBe(
        'Non è stato possibile cercare gli studenti. Riprova più tardi.',
      );
      expect(await chipNames()).toEqual(['Anna Bianchi']);
      expect(await (await select('Ora di inizio')).getValueText()).toBe('10:00');

      // La ricerca successiva funziona ancora.
      await typeMember('verd');
      await respondSearch('verd', [LUCA]);
      expect(element().querySelector('.member-search-error')).toBeNull();
      expect((await optionTextsOfSearch())[0]).toContain('Luca Verdi');
    });

    it('disables the search with 10 members and enables it again after a removal', async () => {
      await render();
      // Uno spazio abbastanza grande per 11 partecipanti.
      for (const student of students(10)) {
        await addMember(student, student.username);
      }

      const search = await memberSearch();
      expect(await search.isDisabled()).toBe(true);
      expect(element().querySelector('.members-field')!.textContent).toContain(
        'Puoi aggiungere al massimo 10 membri.',
      );
      expect(participantsText()).toBe('Partecipanti: 11');

      await removeChip('Studente Prova 3');

      expect(await search.isDisabled()).toBe(false);
      expect(element().querySelector('.members-field')!.textContent).not.toContain(
        'Puoi aggiungere al massimo 10 membri.',
      );
    });

    it('sends the ids of the members with the booking', async () => {
      await render();
      await addMember(ANNA, 'bia');
      await addMember(LUCA, 'ver');

      await fillAndSubmit();

      const request = http.expectOne('/api/bookings');
      expect(request.request.body.memberIds).toEqual(['s2', 's3']);
      request.flush(created({ participants: 3 }), { status: 201, statusText: 'Created' });
    });
  });

  describe('participants and capacity', () => {
    const SMALL_ROOM: RoomDetail = { ...ROOM, name: 'Sala studio B2', capacity: 4 };

    it('shows «Partecipanti: 1» on opening, in a polite live region', async () => {
      await render();

      const participants = element().querySelector('.participants')!;
      expect(participants.getAttribute('aria-live')).toBe('polite');
      expect(participantsText()).toBe('Partecipanti: 1');
    });

    it('shows «Partecipanti: 3» with two members', async () => {
      await render();
      await addMember(ANNA, 'bia');
      await addMember(LUCA, 'ver');

      expect(participantsText()).toBe('Partecipanti: 3');
      expect(capacityWarning()).toBeNull();
    });

    it('warns and sends no request when the participants exceed the capacity', async () => {
      getRoomById.mockReturnValue(of(SMALL_ROOM));
      await render();
      for (const student of students(4)) {
        await addMember(student, student.username);
      }

      expect(participantsText()).toBe('Partecipanti: 5');
      expect(capacityWarning()).toBe('Lo spazio ospita al massimo 4 persone.');
      expect(element().querySelector('.capacity-warning')!.getAttribute('role')).toBe('alert');

      await fillAndSubmit();
      http.expectNone('/api/bookings');
    });

    it('sends the request when the participants equal the capacity', async () => {
      getRoomById.mockReturnValue(of(SMALL_ROOM));
      await render();
      for (const student of students(3)) {
        await addMember(student, student.username);
      }

      expect(participantsText()).toBe('Partecipanti: 4');
      expect(capacityWarning()).toBeNull();
      await fillAndSubmit();
      await respond(created({ participants: 4 }), { status: 201, statusText: 'Created' });
    });

    it('removes the warning when a member is removed', async () => {
      getRoomById.mockReturnValue(of(SMALL_ROOM));
      await render();
      for (const student of students(4)) {
        await addMember(student, student.username);
      }
      expect(capacityWarning()).not.toBeNull();

      await removeChip('Studente Prova 1');

      expect(capacityWarning()).toBeNull();
      expect(participantsText()).toBe('Partecipanti: 4');
    });
  });

  describe('group rejections and confirmation', () => {
    async function bookWithAnna(): Promise<void> {
      await render();
      await addMember(ANNA, 'bia');
      await fillAndSubmit();
    }

    it.each([
      [
        409,
        'members-occupied',
        {},
        'Uno o più membri hanno già una prenotazione in questa fascia.',
      ],
      [
        409,
        'member-booking-in-progress',
        {},
        "Un membro del gruppo ha un'altra prenotazione in corso, riprova tra qualche secondo.",
      ],
      [
        422,
        'capacity-exceeded',
        { limits: { maxParticipants: 4 } },
        'Lo spazio ospita al massimo 4 persone.',
      ],
      [
        422,
        'too-many-members',
        { limits: { maxMembers: 10 } },
        'Puoi aggiungere al massimo 10 membri.',
      ],
      [422, 'members-not-students', {}, 'Puoi aggiungere come membri solo studenti registrati.'],
      [
        422,
        'organizer-as-member',
        {},
        "Non puoi aggiungerti come membro: sei già l'organizzatore.",
      ],
      [422, 'duplicate-members', {}, 'Ogni studente può essere aggiunto una sola volta.'],
      [
        503,
        'members-check-unavailable',
        {},
        'Non è possibile verificare i membri in questo momento. Riprova tra poco.',
      ],
    ])('shows the message of %i «%s» and keeps the chips', async (status, code, extra, message) => {
      await bookWithAnna();

      // Il detail volutamente diverso mostra che il messaggio si sceglie dal codice.
      await respond(...problem(status, 'Rifiuto', 'Testo del servizio', { code, ...extra }));

      expect(alertText()).toBe(message);
      expect(await chipNames()).toEqual(['Anna Bianchi']);
      expect(await (await confirmButton()).isDisabled()).toBe(false);
    });

    it('falls back to the detail when the capacity has no limits', async () => {
      await bookWithAnna();

      await respond(
        ...problem(422, 'Capienza superata', 'Lo spazio ospita al massimo 4 persone.', {
          code: 'capacity-exceeded',
        }),
      );

      expect(alertText()).toBe('Lo spazio ospita al massimo 4 persone.');
    });

    it('shows the 503 detail without a known code', async () => {
      await bookWithAnna();

      await respond(
        ...problem(
          503,
          'Servizio non disponibile',
          'Non è possibile verificare i membri in questo momento. Riprova tra poco.',
        ),
      );

      expect(alertText()).toBe(
        'Non è possibile verificare i membri in questo momento. Riprova tra poco.',
      );
    });

    it('shows the generic message for a 503 without detail', async () => {
      await bookWithAnna();

      await respond(null, { status: 503, statusText: 'Service Unavailable' });

      expect(alertText()).toBe(GENERIC_ERROR);
      expect(await chipNames()).toEqual(['Anna Bianchi']);
    });

    it('lets the student try again after a 503 with the same members', async () => {
      await bookWithAnna();
      await respond(
        ...problem(503, 'Servizio non disponibile', 'x', { code: 'members-check-unavailable' }),
      );

      await (await confirmButton()).click();
      await stable();

      const request = http.expectOne('/api/bookings');
      expect(request.request.body.memberIds).toEqual(['s2']);
      request.flush(created({ participants: 2 }), { status: 201, statusText: 'Created' });
      await stable();
      expect(element().querySelector('.confirmation')).not.toBeNull();
    });

    it('shows «Partecipanti: 3» in the confirmation of a group booking', async () => {
      await render();
      await addMember(ANNA, 'bia');
      await addMember(LUCA, 'ver');
      await fillAndSubmit();

      await respond(created({ participants: 3 }), { status: 201, statusText: 'Created' });

      expect(element().querySelector('.confirmation-participants')!.textContent!.trim()).toBe(
        'Partecipanti: 3',
      );
    });

    it('does not show the participants in the confirmation of a single booking', async () => {
      await render();
      await fillAndSubmit();

      await respond(created({ participants: 1 }), { status: 201, statusText: 'Created' });

      expect(element().querySelector('.confirmation')).not.toBeNull();
      expect(element().querySelector('.confirmation-participants')).toBeNull();
    });

    it("empties the members with «Prenota un'altra fascia»", async () => {
      await bookWithAnna();
      await respond(created({ participants: 2 }), { status: 201, statusText: 'Created' });

      await (
        await loader.getHarness(MatButtonHarness.with({ text: /Prenota un'altra fascia/ }))
      ).click();
      await stable();

      expect(await chipNames()).toEqual([]);
      expect(participantsText()).toBe('Partecipanti: 1');
      expect(element().querySelector('[role="alert"]')).toBeNull();
    });
  });
});

describe('RoomBooking with an expired session', () => {
  // Sessione sintetica salvata come la salva AuthService.
  const SESSION = {
    accessToken: 'token-di-prova',
    // Valida rispetto alla data corrente controllata del test.
    expiresAt: new Date(NOW.getTime() + 60 * 60_000).toISOString(),
    user: { id: 'u1', username: 'student1', displayName: 'Anna Bianchi', role: 'Student' },
  };

  afterEach(() => {
    sessionStorage.clear();
    vi.useRealTimers();
  });

  it('ends the session and goes back to the login on 401', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
    sessionStorage.setItem('smartcampus.session', JSON.stringify(SESSION));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: EmptyPage },
          { path: 'rooms/:id/book', component: RoomBooking },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: RoomsService, useValue: { getRoomById: () => of(ROOM) } },
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/rooms/r1/book');
    const loader = TestbedHarnessEnvironment.loader(harness.fixture);

    const input = await loader.getHarness(MatDatepickerInputHarness);
    await (await loader.getHarness(MatDatepickerToggleHarness)).openCalendar();
    await (await input.getCalendar()).selectCell({ text: '5' });
    for (const [label, time] of [
      ['Ora di inizio', '10:00'],
      ['Ora di fine', '11:00'],
    ]) {
      const field = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
      const control = (await field.getControl(MatSelectHarness))!;
      await control.open();
      await control.clickOptions({ text: time });
    }
    await (await loader.getHarness(MatButtonHarness.with({ text: /Conferma/ }))).click();

    const request = http.expectOne('/api/bookings');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-di-prova');
    request.flush(
      { status: 401, title: 'Accesso richiesto', detail: "Effettua l'accesso per prenotare." },
      { status: 401, statusText: 'Unauthorized' },
    );
    await harness.fixture.whenStable();

    expect(auth.isLoggedIn()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/login?reason=expired&returnUrl=%2Frooms%2Fr1%2Fbook');
    http.match(isAvailabilityRequest);
    http.verify();
  });
});

describe('RoomBooking with a session', () => {
  @Component({ template: '' })
  class LoginPage {}

  // Sessioni sintetiche salvate come le salva AuthService, valide rispetto alla data del test.
  function session(role: 'Student' | 'Staff') {
    return {
      accessToken: 'token-di-prova',
      expiresAt: new Date(NOW.getTime() + 60 * 60_000).toISOString(),
      user:
        role === 'Staff'
          ? { id: 'u9', username: 'staff1', displayName: 'Elena Conti', role }
          : { id: 'u1', username: 'student1', displayName: 'Mario Rossi', role },
    };
  }

  let harness: RouterTestingHarness;
  let loader: HarnessLoader;
  let http: HttpTestingController;

  async function render(role: 'Student' | 'Staff'): Promise<void> {
    sessionStorage.setItem('smartcampus.session', JSON.stringify(session(role)));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: LoginPage },
          { path: 'rooms/:id/book', component: RoomBooking },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: RoomsService, useValue: { getRoomById: () => of(ROOM) } },
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create('/rooms/r1/book');
    loader = TestbedHarnessEnvironment.loader(harness.fixture);
    await harness.fixture.whenStable();
  }

  beforeEach(() => {
    vi.useFakeTimers(FAKE_TIMERS);
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    http?.match(isAvailabilityRequest);
    http?.verify();
    sessionStorage.clear();
    vi.useRealTimers();
  });

  it('hides the members field from Staff and sends memberIds = []', async () => {
    await render('Staff');
    const page = harness.routeNativeElement!;

    expect(page.textContent).not.toContain('Membri del gruppo');
    expect(await loader.getAllHarnesses(MatAutocompleteHarness)).toEqual([]);
    expect(page.querySelector('.participants')!.textContent!.trim()).toBe('Partecipanti: 1');

    await (await loader.getHarness(MatDatepickerToggleHarness)).openCalendar();
    const input = await loader.getHarness(MatDatepickerInputHarness);
    await (await input.getCalendar()).selectCell({ text: '5' });
    for (const [label, time] of [
      ['Ora di inizio', '10:00'],
      ['Ora di fine', '11:00'],
    ]) {
      const field = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
      const control = (await field.getControl(MatSelectHarness))!;
      await control.open();
      await control.clickOptions({ text: time });
    }
    await (await loader.getHarness(MatButtonHarness.with({ text: /Conferma/ }))).click();

    const request = http.expectOne('/api/bookings');
    expect(request.request.body.memberIds).toEqual([]);
    request.flush(
      {
        id: 'p1',
        roomId: 'r1',
        roomName: 'Aula A1',
        start: '2026-11-05T10:00:00+01:00',
        end: '2026-11-05T11:00:00+01:00',
        participants: 1,
        status: 'Confirmed',
      },
      { status: 201, statusText: 'Created' },
    );
  });

  it('ends the session and goes back to the login when the search answers 401', async () => {
    await render('Student');
    const auth = TestBed.inject(AuthService);

    const search = await loader.getHarness(MatAutocompleteHarness);
    await search.enterText('bia');
    vi.advanceTimersByTime(DEBOUNCE_MS);
    await harness.fixture.whenStable();

    const request = http.expectOne((req) => req.url === '/api/identity/students');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-di-prova');
    request.flush(
      { status: 401, title: 'Accesso richiesto' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await harness.fixture.whenStable();

    expect(auth.isLoggedIn()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/login?reason=expired&returnUrl=%2Frooms%2Fr1%2Fbook');
  });
});
