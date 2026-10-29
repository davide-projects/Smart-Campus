import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader, TestKey } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
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
import { ReservationResponse } from '../../models/booking.model';
import { RoomDetail } from '../../models/room.model';
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

const ROOM_CONFLICT = 'Lo spazio è già prenotato in questa fascia.';
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

function problem(status: number, title: string, detail: string): [object, object] {
  return [
    { status, title, detail },
    { status, statusText: title },
  ];
}

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

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
    getRoomById = vi.fn(() => of(ROOM));
  });

  afterEach(() => {
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

    it('links to the availability of the room on a room conflict', async () => {
      await render();
      await fillAndSubmit();

      await respond(...problem(409, 'Spazio già prenotato', ROOM_CONFLICT));

      expect(alertText()).toBe(ROOM_CONFLICT);
      const link = await availabilityLink();
      expect(link!.getAttribute('href')).toBe('/rooms/r1');

      link!.click();
      await stable();
      expect(TestBed.inject(Router).url).toBe('/rooms/r1');
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
    http.verify();
  });
});
