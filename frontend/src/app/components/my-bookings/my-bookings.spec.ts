import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatProgressBarHarness } from '@angular/material/progress-bar/testing';
import { MatTabGroupHarness } from '@angular/material/tabs/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { MyReservationItem, MyReservationsResponse } from '../../models/booking.model';
import { authInterceptor } from '../../services/auth.interceptor';
import { AuthService } from '../../services/auth.service';
import { BookingsService } from '../../services/bookings.service';
import { MyBookings } from './my-bookings';

@Component({ template: '' })
class EmptyPage {}

// Dati sintetici di prova. A novembre Roma è a UTC+1: le 09:00Z sono le 10:00 del campus.
function reservation(overrides: Partial<MyReservationItem>): MyReservationItem {
  return {
    id: 'p1',
    roomId: 'r1',
    roomName: 'Aula A1',
    buildingName: 'Edificio A',
    start: '2026-11-05T09:00:00+00:00',
    end: '2026-11-05T10:00:00+00:00',
    isGroup: false,
    organizerName: 'Anna Bianchi',
    isOrganizer: true,
    ...overrides,
  };
}

const UPCOMING = [
  reservation({ id: 'f1' }),
  reservation({
    id: 'f2',
    roomId: 'r2',
    roomName: 'Sala studio B2',
    buildingName: 'Edificio B',
    start: '2026-11-06T13:00:00+00:00',
    end: '2026-11-06T14:30:00+00:00',
  }),
];

const PAST = [
  reservation({
    id: 'p1',
    roomName: 'Laboratorio C3',
    buildingName: 'Edificio C',
    start: '2026-10-20T08:00:00+00:00',
    end: '2026-10-20T09:00:00+00:00',
  }),
];

const CANCELLED = [
  reservation({
    id: 'c1',
    roomName: 'Aula D4',
    buildingName: 'Edificio D',
    start: '2026-11-10T15:00:00+00:00',
    end: '2026-11-10T16:00:00+00:00',
  }),
];

function response(overrides: Partial<MyReservationsResponse> = {}): MyReservationsResponse {
  return { upcoming: UPCOMING, past: PAST, cancelled: CANCELLED, ...overrides };
}

// Variabile d'ambiente del fuso del processo di test, per simulare un dispositivo in un altro fuso.
const env = (globalThis as unknown as { process: { env: Record<string, string | undefined> } })
  .process.env;

interface Card {
  title: string;
  subtitle: string;
  time: string;
  group: string | null;
  organizer: string | null;
}

describe('MyBookings', () => {
  let fixture: ComponentFixture<MyBookings>;
  let element: HTMLElement;
  let loader: HarnessLoader;
  let getMine: ReturnType<typeof vi.fn<() => Observable<MyReservationsResponse>>>;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [MyBookings],
      providers: [
        provideRouter([{ path: 'rooms', component: EmptyPage }]),
        { provide: BookingsService, useValue: { getMine } },
        // Senza animazioni il contenuto della scheda cambia subito dopo la selezione.
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MyBookings);
    element = fixture.nativeElement;
    loader = TestbedHarnessEnvironment.loader(fixture);
    await fixture.whenStable();
  }

  function text(card: Element, selector: string): string | null {
    return card.querySelector(selector)?.textContent!.trim() ?? null;
  }

  // Le schede non selezionate non sono nel DOM: si leggono solo le voci della scheda aperta.
  function cards(): Card[] {
    return [...element.querySelectorAll('mat-card')].map((card) => ({
      title: text(card, 'mat-card-title')!,
      subtitle: text(card, 'mat-card-subtitle')!,
      time: text(card, '.time')!,
      group: text(card, '.group'),
      organizer: text(card, '.organizer'),
    }));
  }

  function titles(): string[] {
    return cards().map((card) => card.title);
  }

  async function tabs(): Promise<MatTabGroupHarness> {
    return loader.getHarness(MatTabGroupHarness);
  }

  async function selectTab(label: 'Future' | 'Passate' | 'Cancellate'): Promise<void> {
    await (await tabs()).selectTab({ label });
    await fixture.whenStable();
  }

  async function searchLink(): Promise<MatButtonHarness | null> {
    return loader.getHarnessOrNull(MatButtonHarness.with({ text: 'Cerca uno spazio' }));
  }

  beforeEach(() => {
    getMine = vi.fn(() => of(response()));
  });

  describe('tabs', () => {
    it('opens on the Future tab, showing only the upcoming reservations', async () => {
      await render();

      const group = await tabs();
      const labels = await Promise.all((await group.getTabs()).map((tab) => tab.getLabel()));
      expect(labels).toEqual(['Future', 'Passate', 'Cancellate']);
      expect(await (await group.getSelectedTab()).getLabel()).toBe('Future');
      expect(titles()).toEqual(['Aula A1', 'Sala studio B2']);
    });

    it('shows room, building, day and time of each reservation in the API order', async () => {
      await render();

      expect(cards()).toEqual([
        {
          title: 'Aula A1',
          subtitle: 'Edificio A',
          time: 'giovedì 5 novembre 2026, 10:00–11:00',
          group: null,
          organizer: null,
        },
        {
          title: 'Sala studio B2',
          subtitle: 'Edificio B',
          time: 'venerdì 6 novembre 2026, 14:00–15:30',
          group: null,
          organizer: null,
        },
      ]);
    });

    it('keeps the order received from the API', async () => {
      getMine.mockReturnValue(of(response({ upcoming: [UPCOMING[1], UPCOMING[0]] })));
      await render();

      expect(titles()).toEqual(['Sala studio B2', 'Aula A1']);
    });

    it('shows only the past reservations on the Passate tab', async () => {
      await render();

      await selectTab('Passate');

      expect(cards()).toEqual([
        {
          title: 'Laboratorio C3',
          subtitle: 'Edificio C',
          time: 'martedì 20 ottobre 2026, 10:00–11:00',
          group: null,
          organizer: null,
        },
      ]);
    });

    it('shows only the cancelled reservations on the Cancellate tab', async () => {
      await render();

      await selectTab('Cancellate');

      expect(cards()).toEqual([
        {
          title: 'Aula D4',
          subtitle: 'Edificio D',
          time: 'martedì 10 novembre 2026, 16:00–17:00',
          group: null,
          organizer: null,
        },
      ]);
    });

    it('shows a dash when the building is not known', async () => {
      getMine.mockReturnValue(of(response({ upcoming: [reservation({ buildingName: null })] })));
      await render();

      expect(cards()[0].subtitle).toBe('—');
    });
  });

  describe('day and time', () => {
    const originalTimeZone = env['TZ'];

    afterEach(() => {
      if (originalTimeZone === undefined) {
        delete env['TZ'];
      } else {
        env['TZ'] = originalTimeZone;
      }
    });

    it('uses the campus time even when the device is in another time zone', async () => {
      env['TZ'] = 'UTC';
      // Il fuso del dispositivo è davvero cambiato: in UTC l'inizio è alle 9.
      expect(new Date(UPCOMING[0].start).getHours()).toBe(9);
      await render();

      expect(cards()[0].time).toBe('giovedì 5 novembre 2026, 10:00–11:00');
    });

    it('uses the campus time on a device far from Rome', async () => {
      env['TZ'] = 'America/New_York';
      expect(new Date(UPCOMING[0].start).getHours()).toBe(4);
      await render();

      expect(cards()[0].time).toBe('giovedì 5 novembre 2026, 10:00–11:00');
    });

    it('follows daylight saving time in Rome', async () => {
      // A luglio Roma è a UTC+2.
      getMine.mockReturnValue(
        of(
          response({
            upcoming: [
              reservation({ start: '2026-07-02T08:00:00+00:00', end: '2026-07-02T09:00:00+00:00' }),
            ],
          }),
        ),
      );
      await render();

      expect(cards()[0].time).toBe('giovedì 2 luglio 2026, 10:00–11:00');
    });
  });

  describe('group reservations', () => {
    async function renderOne(item: Partial<MyReservationItem>): Promise<Card> {
      getMine.mockReturnValue(of(response({ upcoming: [reservation(item)] })));
      await render();
      return cards()[0];
    }

    it('shows the organizer to a member of the group', async () => {
      const card = await renderOne({
        isGroup: true,
        isOrganizer: false,
        organizerName: 'Mario Rossi',
      });

      expect(card.group).toBe('Prenotazione di gruppo');
      expect(card.organizer).toBe('Organizzatore: Mario Rossi');
    });

    it('shows "Tu" to the organizer of the group', async () => {
      const card = await renderOne({ isGroup: true, isOrganizer: true, organizerName: null });

      expect(card.group).toBe('Prenotazione di gruppo');
      expect(card.organizer).toBe('Organizzatore: Tu');
    });

    it('says when the organizer is not known', async () => {
      const card = await renderOne({ isGroup: true, isOrganizer: false, organizerName: null });

      expect(card.group).toBe('Prenotazione di gruppo');
      expect(card.organizer).toBe('Organizzatore non disponibile');
    });

    it('shows no group label and no organizer on a single reservation', async () => {
      const card = await renderOne({ isGroup: false, organizerName: 'Mario Rossi' });

      expect(card.group).toBeNull();
      expect(card.organizer).toBeNull();
      expect(element.textContent).not.toContain('Organizzatore');
    });
  });

  describe('empty tabs', () => {
    it('suggests searching a room when there are no upcoming reservations', async () => {
      getMine.mockReturnValue(of(response({ upcoming: [] })));
      await render();

      expect(element.querySelector('[role="status"]')!.textContent).toContain(
        'Non hai prenotazioni future.',
      );
      const link = element.querySelector<HTMLAnchorElement>('[role="status"] a')!;
      expect(link.textContent!.trim()).toBe('Cerca uno spazio');
      expect(link.getAttribute('href')).toBe('/rooms');
    });

    it('opens the room search from the link', async () => {
      getMine.mockReturnValue(of(response({ upcoming: [] })));
      await render();

      await (await searchLink())!.click();
      await fixture.whenStable();

      expect(TestBed.inject(Router).url).toBe('/rooms');
    });

    it('shows its own message without link when there are no past reservations', async () => {
      getMine.mockReturnValue(of(response({ past: [] })));
      await render();

      await selectTab('Passate');

      expect(element.querySelector('[role="status"]')!.textContent!.trim()).toBe(
        'Non hai prenotazioni passate.',
      );
      expect(await searchLink()).toBeNull();
    });

    it('shows its own message without link when there are no cancelled reservations', async () => {
      getMine.mockReturnValue(of(response({ cancelled: [] })));
      await render();

      await selectTab('Cancellate');

      expect(element.querySelector('[role="status"]')!.textContent!.trim()).toBe(
        'Non hai prenotazioni cancellate.',
      );
      expect(await searchLink()).toBeNull();
    });

    it('shows the past reservations even when there are no upcoming ones', async () => {
      getMine.mockReturnValue(of(response({ upcoming: [] })));
      await render();

      expect(element.textContent).toContain('Non hai prenotazioni future.');
      expect(cards()).toEqual([]);

      await selectTab('Passate');

      expect(titles()).toEqual(['Laboratorio C3']);
      expect(element.textContent).not.toContain('Non hai prenotazioni');
    });
  });

  describe('loading and errors', () => {
    it('shows a progress bar while loading', async () => {
      const pending = new Subject<MyReservationsResponse>();
      getMine.mockReturnValue(pending);
      await render();

      expect(await loader.hasHarness(MatProgressBarHarness)).toBe(true);
      expect(await loader.hasHarness(MatTabGroupHarness)).toBe(false);

      pending.next(response());
      pending.complete();
      await fixture.whenStable();

      expect(await loader.hasHarness(MatProgressBarHarness)).toBe(false);
      expect(await loader.hasHarness(MatTabGroupHarness)).toBe(true);
    });

    it('shows an error without tabs when the service fails', async () => {
      getMine.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Server Error' })),
      );
      await render();

      expect(element.querySelector('[role="alert"]')!.textContent!.trim()).toBe(
        'Non è stato possibile caricare le prenotazioni. Riprova più tardi.',
      );
      expect(await loader.hasHarness(MatTabGroupHarness)).toBe(false);
      expect(await loader.hasHarness(MatProgressBarHarness)).toBe(false);
      expect(element.textContent).not.toContain('Non hai prenotazioni future.');
    });

    it('shows the same error when the service does not respond', async () => {
      getMine.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
      await render();

      expect(element.querySelector('[role="alert"]')).not.toBeNull();
      expect(element.textContent).not.toContain('Non hai prenotazioni future.');
    });
  });
});

describe('MyBookings with an expired session', () => {
  // Sessione sintetica salvata come la salva AuthService.
  const SESSION = {
    accessToken: 'token-di-prova',
    expiresAt: new Date(Date.now() + 60 * 60_000).toISOString(),
    user: { id: 'u1', username: 'student1', displayName: 'Anna Bianchi', role: 'Student' },
  };

  afterEach(() => sessionStorage.clear());

  it('ends the session and goes back to the login on 401', async () => {
    sessionStorage.setItem('smartcampus.session', JSON.stringify(SESSION));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: EmptyPage },
          { path: 'my-bookings', component: MyBookings },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/my-bookings');

    const request = http.expectOne('/api/bookings/mine');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-di-prova');
    request.flush(null, { status: 401, statusText: 'Unauthorized' });
    await harness.fixture.whenStable();

    expect(auth.isLoggedIn()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/login?reason=expired&returnUrl=%2Fmy-bookings');
    http.verify();
  });
});
