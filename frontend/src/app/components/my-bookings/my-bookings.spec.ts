import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader, TestKey } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSnackBarHarness } from '@angular/material/snack-bar/testing';
import { MatProgressBarHarness } from '@angular/material/progress-bar/testing';
import { MatTabGroupHarness } from '@angular/material/tabs/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import {
  CheckInResponse,
  MyReservationItem,
  MyReservationsResponse,
} from '../../models/booking.model';
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
    checkedInAt: null,
    members: [],
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

// Istante sintetico del check-in, come lo invia Bookings (UTC).
const CHECKED_IN_AT = '2026-11-05T08:55:00+00:00';

const HINT = "Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.";

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
  // Dialog e snackbar stanno nell'overlay, fuori dal componente.
  let rootLoader: HarnessLoader;
  let getMine: ReturnType<typeof vi.fn<() => Observable<MyReservationsResponse>>>;
  let cancel: ReturnType<typeof vi.fn<(id: string) => Observable<void>>>;
  let checkIn: ReturnType<typeof vi.fn<(id: string) => Observable<CheckInResponse>>>;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [MyBookings],
      providers: [
        provideRouter([{ path: 'rooms', component: EmptyPage }]),
        { provide: BookingsService, useValue: { getMine, cancel, checkIn } },
        // Senza animazioni il contenuto della scheda cambia subito dopo la selezione.
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MyBookings);
    element = fixture.nativeElement;
    loader = TestbedHarnessEnvironment.loader(fixture);
    rootLoader = TestbedHarnessEnvironment.documentRootLoader(fixture);
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

  // Pulsanti «Cancella» della scheda aperta (il testo esatto esclude «Cancella prenotazione»).
  async function cancelButtons(): Promise<MatButtonHarness[]> {
    return loader.getAllHarnesses(MatButtonHarness.with({ text: 'Cancella' }));
  }

  // Pulsanti «Check-in» della scheda aperta.
  async function checkInButtons(): Promise<MatButtonHarness[]> {
    return loader.getAllHarnesses(MatButtonHarness.with({ text: 'Check-in' }));
  }

  // Titoli delle voci della scheda aperta che mostrano «Cancella».
  function cancellableTitles(): string[] {
    return [...element.querySelectorAll('mat-card')]
      .filter((card) => card.querySelector('button.cancel') !== null)
      .map((card) => text(card, 'mat-card-title')!);
  }

  // Ora del dispositivo controllata: il 1° novembre 2026 alle 12:00Z.
  const NOW = new Date('2026-11-01T12:00:00Z');

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
    getMine = vi.fn(() => of(response()));
    cancel = vi.fn(() => of(undefined));
    checkIn = vi.fn(() => of({ checkedInAt: CHECKED_IN_AT }));
  });

  afterEach(() => {
    TestBed.inject(MatDialog).closeAll();
    vi.useRealTimers();
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

    it('lists the members of the group under the organizer', async () => {
      await renderOne({
        isGroup: true,
        isOrganizer: false,
        organizerName: 'Mario Rossi',
        members: [
          { id: 'u2', displayName: 'Anna Bianchi' },
          { id: 'u3', displayName: 'Luca Verdi' },
        ],
      });

      const organizer = element.querySelector('.organizer')!;
      expect(organizer.nextElementSibling?.classList).toContain('members');
      expect(membersText()).toBe('Membri: Anna Bianchi, Luca Verdi');
    });

    it('shows a fallback for a member whose name is not known', async () => {
      await renderOne({
        isGroup: true,
        members: [
          { id: 'u2', displayName: 'Anna Bianchi' },
          { id: 'u3', displayName: null },
        ],
      });

      expect(membersText()).toBe('Membri: Anna Bianchi, Nome non disponibile');
    });

    it('shows no members line on a single reservation', async () => {
      await renderOne({ isGroup: false, members: [] });

      expect(element.querySelector('.members')).toBeNull();
      expect(element.textContent).not.toContain('Membri');
    });

    function membersText(): string | null | undefined {
      return element.querySelector('.members')?.textContent?.trim();
    }
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

  describe('cancel button', () => {
    it('is shown on the upcoming reservations of the organizer', async () => {
      await render();

      expect(cancellableTitles()).toEqual(['Aula A1', 'Sala studio B2']);
      const button = element.querySelector('button.cancel')!;
      expect(button.textContent!.trim()).toBe('Cancella');
      expect(button.getAttribute('aria-label')).toBe(
        'Cancella la prenotazione di Aula A1, giovedì 5 novembre 2026, 10:00–11:00',
      );
    });

    it('is not shown on a reservation where the user is only a member', async () => {
      getMine.mockReturnValue(
        of(
          response({
            upcoming: [
              reservation({ id: 'f1', isGroup: true, isOrganizer: false, organizerName: 'Mario' }),
              reservation({ id: 'f2', roomName: 'Sala studio B2' }),
            ],
          }),
        ),
      );
      await render();

      expect(cancellableTitles()).toEqual(['Sala studio B2']);
    });

    it('is not shown on a reservation of the organizer that has already started', async () => {
      // Le 10:30 del campus, a metà della prenotazione dalle 10:00 alle 11:00.
      vi.setSystemTime(new Date('2026-11-05T09:30:00Z'));
      await render();

      expect(cancellableTitles()).toEqual(['Sala studio B2']);
    });

    it('is not shown on the Passate and Cancellate tabs', async () => {
      await render();

      await selectTab('Passate');
      expect(titles()).toEqual(['Laboratorio C3']);
      expect(await cancelButtons()).toEqual([]);

      await selectTab('Cancellate');
      expect(titles()).toEqual(['Aula D4']);
      expect(await cancelButtons()).toEqual([]);
    });
  });

  describe('cancelling', () => {
    // Risposta dopo la cancellazione dell'«Aula A1».
    const AFTER_CANCEL = response({
      upcoming: [UPCOMING[1]],
      cancelled: [UPCOMING[0], ...CANCELLED],
    });

    async function openDialog(index = 0): Promise<MatButtonHarness> {
      const button = (await cancelButtons())[index];
      await button.focus();
      await button.click();
      await fixture.whenStable();
      return button;
    }

    async function confirmInDialog(): Promise<void> {
      await (
        await rootLoader.getHarness(MatButtonHarness.with({ text: 'Cancella prenotazione' }))
      ).click();
      await fixture.whenStable();
    }

    async function selectedLabel(): Promise<string> {
      return (await (await tabs()).getSelectedTab()).getLabel();
    }

    function futureTabLabel(): HTMLElement {
      const label = element.querySelector<HTMLElement>('[role="tab"]')!;
      expect(label.textContent).toContain('Future');
      return label;
    }

    it('opens the dialog with the summary of the reservation', async () => {
      await render();

      await openDialog(1);

      const dialog = await rootLoader.getHarness(MatDialogHarness);
      expect(await dialog.getTitleText()).toBe('Cancellare la prenotazione?');
      expect(await dialog.getContentText()).toContain('Sala studio B2');
      expect(await dialog.getContentText()).toContain('venerdì 6 novembre 2026, 14:00–15:30');
    });

    it('reloads the list and shows the reservation in Cancellate after the confirmation', async () => {
      getMine.mockReturnValueOnce(of(response())).mockReturnValueOnce(of(AFTER_CANCEL));
      await render();

      await openDialog();
      await confirmInDialog();

      expect(cancel).toHaveBeenCalledExactlyOnceWith('f1');
      expect(getMine).toHaveBeenCalledTimes(2);
      expect(await rootLoader.hasHarness(MatDialogHarness)).toBe(false);
      expect(await selectedLabel()).toBe('Future');
      expect(titles()).toEqual(['Sala studio B2']);

      await selectTab('Cancellate');
      expect(cards()[0]).toEqual({
        title: 'Aula A1',
        subtitle: 'Edificio A',
        time: 'giovedì 5 novembre 2026, 10:00–11:00',
        group: null,
        organizer: null,
      });
      expect(titles()).toEqual(['Aula A1', 'Aula D4']);
    });

    it('shows the empty Future tab after cancelling the last upcoming reservation', async () => {
      getMine
        .mockReturnValueOnce(of(response({ upcoming: [UPCOMING[0]] })))
        .mockReturnValueOnce(of(response({ upcoming: [], cancelled: [UPCOMING[0]] })));
      await render();

      await openDialog();
      await confirmInDialog();

      expect(await selectedLabel()).toBe('Future');
      expect(element.querySelector('[role="status"]')!.textContent).toContain(
        'Non hai prenotazioni future.',
      );
      expect(await searchLink()).not.toBeNull();
    });

    it('keeps the tabs and the previous items while reloading', async () => {
      const pending = new Subject<MyReservationsResponse>();
      getMine.mockReturnValueOnce(of(response())).mockReturnValueOnce(pending);
      await render();

      await openDialog();
      await confirmInDialog();

      expect(getMine).toHaveBeenCalledTimes(2);
      expect(await loader.hasHarness(MatTabGroupHarness)).toBe(true);
      expect(await loader.hasHarness(MatProgressBarHarness)).toBe(false);
      expect(await selectedLabel()).toBe('Future');
      expect(titles()).toEqual(['Aula A1', 'Sala studio B2']);

      pending.next(AFTER_CANCEL);
      pending.complete();
      await fixture.whenStable();
      expect(titles()).toEqual(['Sala studio B2']);
    });

    it('shows a snackbar without actions configured for 5 seconds', async () => {
      getMine.mockReturnValueOnce(of(response())).mockReturnValueOnce(of(AFTER_CANCEL));
      await render();
      const open = vi.spyOn(TestBed.inject(MatSnackBar), 'open');

      await openDialog();
      await confirmInDialog();

      expect(open).toHaveBeenCalledExactlyOnceWith('Prenotazione cancellata.', undefined, {
        duration: 5000,
      });
      const snackBar = await rootLoader.getHarness(MatSnackBarHarness);
      expect(await snackBar.getMessage()).toBe('Prenotazione cancellata.');
      expect(await snackBar.hasAction()).toBe(false);
    });

    it('closes the snackbar by itself after 5 seconds', async () => {
      // shouldAdvanceTime serve: senza, gli harness e le animazioni di Material restano in attesa del
      // tempo reale e il test va in timeout. Il margine di un secondo tra 4000 e 5500 ms assorbe
      // l'avanzamento reale durante il test.
      vi.useFakeTimers({
        toFake: ['Date', 'setTimeout', 'clearTimeout'],
        shouldAdvanceTime: true,
      });
      vi.setSystemTime(NOW);
      getMine.mockReturnValueOnce(of(response())).mockReturnValueOnce(of(AFTER_CANCEL));
      await render();

      await openDialog();
      await confirmInDialog();
      expect(await rootLoader.hasHarness(MatSnackBarHarness)).toBe(true);

      await vi.advanceTimersByTimeAsync(4000);
      await fixture.whenStable();
      expect(await rootLoader.hasHarness(MatSnackBarHarness)).toBe(true);

      await vi.advanceTimersByTimeAsync(1500);
      await fixture.whenStable();
      expect(await rootLoader.hasHarness(MatSnackBarHarness)).toBe(false);
    });

    it('moves the focus to the Future tab label', async () => {
      getMine.mockReturnValueOnce(of(response())).mockReturnValueOnce(of(AFTER_CANCEL));
      await render();

      await openDialog();
      await confirmInDialog();

      expect(document.activeElement).toBe(futureTabLabel());
    });

    it('keeps tabs and items and shows an alert above the tabs when the reload fails', async () => {
      getMine
        .mockReturnValueOnce(of(response()))
        .mockReturnValueOnce(
          throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Server Error' })),
        );
      await render();

      await openDialog();
      await confirmInDialog();

      const alert = element.querySelector('[role="alert"]')!;
      expect(alert.textContent!.trim()).toBe(
        'Non è stato possibile caricare le prenotazioni. Riprova più tardi.',
      );
      expect(alert.nextElementSibling!.tagName).toBe('MAT-TAB-GROUP');
      expect(await selectedLabel()).toBe('Future');
      expect(titles()).toEqual(['Aula A1', 'Sala studio B2']);
      expect(await loader.hasHarness(MatProgressBarHarness)).toBe(false);
    });

    it('does not reload and gives the focus back to Cancella when closed with Annulla', async () => {
      await render();

      const button = await openDialog();
      await (await rootLoader.getHarness(MatButtonHarness.with({ text: 'Annulla' }))).click();
      await fixture.whenStable();

      expect(await rootLoader.hasHarness(MatDialogHarness)).toBe(false);
      expect(cancel).not.toHaveBeenCalled();
      expect(getMine).toHaveBeenCalledTimes(1);
      expect(await button.isFocused()).toBe(true);
      expect(titles()).toEqual(['Aula A1', 'Sala studio B2']);
    });
  });

  describe('used label', () => {
    // Le 10:30 del campus, durante la prenotazione dell'«Aula A1» dalle 10:00 alle 11:00.
    const DURING = new Date('2026-11-05T09:30:00Z');

    function usedLabel(card: Element): HTMLElement | null {
      return card.querySelector<HTMLElement>('.used');
    }

    it('is shown on an upcoming reservation with check-in, without Check-in and Cancella', async () => {
      vi.setSystemTime(DURING);
      getMine.mockReturnValue(
        of(response({ upcoming: [reservation({ id: 'f1', checkedInAt: CHECKED_IN_AT })] })),
      );
      await render();

      const card = element.querySelector('mat-card')!;
      expect(usedLabel(card)!.textContent!.trim()).toBe('Usata');
      expect(usedLabel(card)!.getAttribute('tabindex')).toBe('-1');
      expect(await checkInButtons()).toEqual([]);
      expect(await cancelButtons()).toEqual([]);
    });

    it('is shown on a past reservation with check-in', async () => {
      getMine.mockReturnValue(
        of(response({ past: [reservation({ ...PAST[0], checkedInAt: CHECKED_IN_AT })] })),
      );
      await render();

      await selectTab('Passate');

      expect(usedLabel(element.querySelector('mat-card')!)!.textContent!.trim()).toBe('Usata');
    });

    it('is not shown on reservations without check-in', async () => {
      await render();

      expect(element.querySelectorAll('.used').length).toBe(0);
      expect(element.textContent).not.toContain('Usata');

      await selectTab('Passate');
      expect(titles()).toEqual(['Laboratorio C3']);
      expect(element.textContent).not.toContain('Usata');
    });

    it('is shown to a member when another member of the group checked in', async () => {
      vi.setSystemTime(new Date('2026-11-05T08:50:00Z'));
      getMine.mockReturnValue(
        of(
          response({
            upcoming: [
              reservation({
                isGroup: true,
                isOrganizer: false,
                organizerName: 'Mario Rossi',
                checkedInAt: CHECKED_IN_AT,
              }),
            ],
          }),
        ),
      );
      await render();

      expect(usedLabel(element.querySelector('mat-card')!)!.textContent!.trim()).toBe('Usata');
      expect(await checkInButtons()).toEqual([]);
    });

    it('hides Cancella on an upcoming reservation of the organizer with check-in', async () => {
      vi.setSystemTime(new Date('2026-11-05T08:50:00Z'));
      getMine.mockReturnValue(
        of(
          response({
            upcoming: [
              reservation({ id: 'f1', checkedInAt: CHECKED_IN_AT }),
              reservation({ id: 'f2', roomName: 'Sala studio B2' }),
            ],
          }),
        ),
      );
      await render();

      expect(cancellableTitles()).toEqual(['Sala studio B2']);
    });
  });

  describe('check-in button', () => {
    async function renderAt(now: string, item: Partial<MyReservationItem> = {}): Promise<void> {
      vi.setSystemTime(new Date(now));
      getMine.mockReturnValue(of(response({ upcoming: [reservation({ id: 'f1', ...item })] })));
      await render();
    }

    async function onlyCheckInButton(): Promise<MatButtonHarness> {
      const buttons = await checkInButtons();
      expect(buttons.length).toBe(1);
      return buttons[0];
    }

    function hint(): HTMLElement | null {
      return element.querySelector<HTMLElement>('.check-in-hint');
    }

    async function expectEnabled(): Promise<void> {
      const button = await onlyCheckInButton();
      expect(await button.isDisabled()).toBe(false);
      expect(hint()).toBeNull();
      expect(element.textContent).not.toContain(HINT);
      expect(await (await button.host()).getAttribute('aria-describedby')).toBeNull();
    }

    async function expectDisabledWithHint(): Promise<void> {
      const button = await onlyCheckInButton();
      expect(await button.isDisabled()).toBe(true);
      expect(hint()!.textContent!.trim()).toBe(HINT);
      const describedBy = await (await button.host()).getAttribute('aria-describedby');
      expect(describedBy).toBe(hint()!.id);
      expect(document.getElementById(describedBy!)!.textContent!.trim()).toBe(HINT);
      // Disabilitato ma raggiungibile con Tab.
      await button.focus();
      expect(await button.isFocused()).toBe(true);
    }

    it('is enabled in the window with the expected accessible name', async () => {
      // Le 09:50 del campus.
      await renderAt('2026-11-05T08:50:00Z');

      await expectEnabled();
      const button = await onlyCheckInButton();
      expect(await button.getText()).toBe('Check-in');
      expect(await (await button.host()).getAttribute('aria-label')).toBe(
        'Check-in per Aula A1, giovedì 5 novembre 2026, 10:00–11:00',
      );
    });

    it('is enabled for a member of a group reservation in the window', async () => {
      // Le 10:10 del campus.
      await renderAt('2026-11-05T09:10:00Z', {
        isGroup: true,
        isOrganizer: false,
        organizerName: 'Mario Rossi',
      });

      await expectEnabled();
      expect(await cancelButtons()).toEqual([]);
    });

    it('is enabled exactly 15 minutes before the start', async () => {
      await renderAt('2026-11-05T08:45:00Z');

      await expectEnabled();
    });

    it('is enabled exactly 15 minutes after the start', async () => {
      await renderAt('2026-11-05T09:15:00Z');

      await expectEnabled();
    });

    it('is disabled with the explanation on a reservation of tomorrow', async () => {
      await renderAt('2026-11-04T09:00:00Z');

      await expectDisabledWithHint();
    });

    it('is disabled with the explanation an instant before the window', async () => {
      await renderAt('2026-11-05T08:44:59.999Z');

      await expectDisabledWithHint();
    });

    it('is disabled with the explanation on a reservation in progress past the window', async () => {
      // Le 10:20 del campus.
      await renderAt('2026-11-05T09:20:00Z');

      await expectDisabledWithHint();
    });

    it('sends no request when the disabled button is clicked or activated from the keyboard', async () => {
      await renderAt('2026-11-04T09:00:00Z');
      const button = await onlyCheckInButton();

      // jsdom non trasforma Invio e Spazio in un clic come fa il browser: il caso è coperto da
      // button.click(), e la verifica da tastiera è nella prova manuale 2.5.
      await button.click();
      await button.focus();
      await (await button.host()).sendKeys(TestKey.ENTER);
      await (await button.host()).sendKeys(' ');
      await fixture.whenStable();

      expect(checkIn).not.toHaveBeenCalled();
    });

    it('is not shown on the Passate and Cancellate tabs', async () => {
      await renderAt('2026-11-05T08:50:00Z');

      await selectTab('Passate');
      expect(titles()).toEqual(['Laboratorio C3']);
      expect(await checkInButtons()).toEqual([]);

      await selectTab('Cancellate');
      expect(titles()).toEqual(['Aula D4']);
      expect(await checkInButtons()).toEqual([]);
    });

    it('is shown together with Cancella on an upcoming reservation of the organizer', async () => {
      await renderAt('2026-11-05T08:50:00Z');

      expect(await checkInButtons()).toHaveLength(1);
      expect(await cancelButtons()).toHaveLength(1);
    });
  });

  describe('check-in window while the page is open', () => {
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    });

    async function renderAt(now: string): Promise<void> {
      vi.setSystemTime(new Date(now));
      getMine.mockReturnValue(of(response({ upcoming: [reservation({ id: 'f1' })] })));
      await render();
    }

    async function isDisabled(): Promise<boolean> {
      return (await checkInButtons())[0].isDisabled();
    }

    it('enables the button when the window opens, without reloading the list', async () => {
      // Le 09:44:40 del campus.
      await renderAt('2026-11-05T08:44:40Z');
      expect(await isDisabled()).toBe(true);

      vi.advanceTimersByTime(30_000);
      await fixture.whenStable();

      expect(await isDisabled()).toBe(false);
      expect(element.querySelector('.check-in-hint')).toBeNull();
      expect(
        await (await (await checkInButtons())[0].host()).getAttribute('aria-describedby'),
      ).toBeNull();
      expect(getMine).toHaveBeenCalledTimes(1);
      expect(checkIn).not.toHaveBeenCalled();
    });

    it('disables the button when the window closes', async () => {
      // Le 10:14:50 del campus.
      await renderAt('2026-11-05T09:14:50Z');
      expect(await isDisabled()).toBe(false);

      vi.advanceTimersByTime(30_000);
      await fixture.whenStable();

      expect(await isDisabled()).toBe(true);
      expect(element.querySelector('.check-in-hint')!.textContent!.trim()).toBe(HINT);
      expect(getMine).toHaveBeenCalledTimes(1);
    });

    it('stops the periodic refresh when the page is destroyed', async () => {
      const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');
      const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');
      await renderAt('2026-11-05T08:44:40Z');

      // Il timer del ricalcolo: jsdom usa setInterval anche per requestAnimationFrame.
      const index = setIntervalSpy.mock.calls.findIndex(([, delay]) => delay === 30_000);
      expect(index).toBeGreaterThanOrEqual(0);
      const handle = setIntervalSpy.mock.results[index].value;
      expect(clearIntervalSpy).not.toHaveBeenCalledWith(handle);

      fixture.destroy();

      expect(clearIntervalSpy).toHaveBeenCalledWith(handle);
    });
  });

  describe('checking in', () => {
    // Le 09:55 del campus, nella finestra dell'«Aula A1» delle 10:00.
    const IN_WINDOW = new Date('2026-11-05T08:55:00Z');

    beforeEach(() => vi.setSystemTime(IN_WINDOW));

    async function clickCheckIn(): Promise<void> {
      const button = (await checkInButtons())[0];
      await button.focus();
      await button.click();
      await fixture.whenStable();
    }

    function firstCard(): Element {
      return element.querySelector('mat-card')!;
    }

    function problem(status: number, detail: string): HttpErrorResponse {
      return new HttpErrorResponse({
        status,
        error: { title: 'Check-in non consentito', detail },
      });
    }

    it('sends the request at once, without a confirmation dialog', async () => {
      await render();

      await clickCheckIn();

      expect(checkIn).toHaveBeenCalledExactlyOnceWith('f1');
      expect(await rootLoader.hasHarness(MatDialogHarness)).toBe(false);
    });

    it('sends a single request when activated twice while the request is pending', async () => {
      const pending = new Subject<CheckInResponse>();
      checkIn.mockReturnValue(pending);
      await render();

      const button = (await checkInButtons())[0];
      await button.click();
      await fixture.whenStable();
      expect(await button.isDisabled()).toBe(true);
      await button.click();
      await fixture.whenStable();

      expect(checkIn).toHaveBeenCalledTimes(1);
      expect(await rootLoader.hasHarness(MatDialogHarness)).toBe(false);
    });

    it('shows Usata without Check-in and Cancella and does not reload the list', async () => {
      await render();
      expect(cancellableTitles()).toEqual(['Aula A1', 'Sala studio B2']);

      await clickCheckIn();

      const card = firstCard();
      expect(text(card, 'mat-card-title')).toBe('Aula A1');
      expect(text(card, '.used')).toBe('Usata');
      expect(card.querySelector('button')).toBeNull();
      expect(cancellableTitles()).toEqual(['Sala studio B2']);
      expect(titles()).toEqual(['Aula A1', 'Sala studio B2']);
      expect(await (await (await tabs()).getSelectedTab()).getLabel()).toBe('Future');
      expect(getMine).toHaveBeenCalledTimes(1);
    });

    it('shows a snackbar without actions configured for 5 seconds', async () => {
      await render();
      const open = vi.spyOn(TestBed.inject(MatSnackBar), 'open');

      await clickCheckIn();

      expect(open).toHaveBeenCalledExactlyOnceWith('Check-in registrato.', undefined, {
        duration: 5000,
      });
      const snackBar = await rootLoader.getHarness(MatSnackBarHarness);
      expect(await snackBar.getMessage()).toBe('Check-in registrato.');
      expect(await snackBar.hasAction()).toBe(false);
    });

    it('closes the snackbar by itself after 5 seconds', async () => {
      // Stesso schema del test della snackbar di cancellazione.
      vi.useFakeTimers({
        toFake: ['Date', 'setTimeout', 'clearTimeout'],
        shouldAdvanceTime: true,
      });
      vi.setSystemTime(IN_WINDOW);
      await render();

      await clickCheckIn();
      expect(await rootLoader.hasHarness(MatSnackBarHarness)).toBe(true);

      await vi.advanceTimersByTimeAsync(4000);
      await fixture.whenStable();
      expect(await rootLoader.hasHarness(MatSnackBarHarness)).toBe(true);

      await vi.advanceTimersByTimeAsync(1500);
      await fixture.whenStable();
      expect(await rootLoader.hasHarness(MatSnackBarHarness)).toBe(false);
    });

    it('moves the focus to the Usata label of the same reservation', async () => {
      await render();

      await clickCheckIn();

      const used = firstCard().querySelector('.used');
      expect(used).not.toBeNull();
      expect(document.activeElement).toBe(used);
    });

    it.each([
      [403, 'Non puoi fare il check-in di questa prenotazione.'],
      [404, 'La prenotazione richiesta non esiste.'],
      [422, HINT],
      [422, 'Il check-in di questa prenotazione è già stato registrato.'],
      [422, 'Non è possibile fare il check-in di una prenotazione cancellata.'],
    ])('shows the detail of the %i response in the card', async (status, detail) => {
      checkIn.mockReturnValue(throwError(() => problem(status, detail)));
      await render();

      await clickCheckIn();

      const alert = firstCard().querySelector('[role="alert"]')!;
      expect(alert.textContent!.trim()).toBe(detail);
      expect(firstCard().querySelector('.used')).toBeNull();
      expect(await (await checkInButtons())[0].isDisabled()).toBe(false);
      expect(element.querySelectorAll('[role="alert"]').length).toBe(1);
    });

    it.each([
      ['a 500 response', new HttpErrorResponse({ status: 500, statusText: 'Server Error' })],
      ['no response', new HttpErrorResponse({ status: 0 })],
    ])('shows the generic message on %s', async (_case, error) => {
      checkIn.mockReturnValue(throwError(() => error));
      await render();

      await clickCheckIn();

      expect(firstCard().querySelector('[role="alert"]')!.textContent!.trim()).toBe(
        'Non è stato possibile registrare il check-in. Riprova più tardi.',
      );
      expect(firstCard().querySelector('.used')).toBeNull();
    });

    it('removes the alert on the next attempt', async () => {
      const pending = new Subject<CheckInResponse>();
      checkIn
        .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 500 })))
        .mockReturnValueOnce(pending);
      await render();

      await clickCheckIn();
      expect(firstCard().querySelector('[role="alert"]')).not.toBeNull();

      await clickCheckIn();
      expect(checkIn).toHaveBeenCalledTimes(2);
      expect(firstCard().querySelector('[role="alert"]')).toBeNull();
    });

    it('keeps the other reservations active while one check-in is pending', async () => {
      const first = new Subject<CheckInResponse>();
      const second = new Subject<CheckInResponse>();
      checkIn.mockReturnValueOnce(first).mockReturnValueOnce(second);
      getMine.mockReturnValue(
        of(
          response({
            upcoming: [
              reservation({ id: 'f1' }),
              reservation({ id: 'f2', roomId: 'r2', roomName: 'Sala studio B2' }),
            ],
          }),
        ),
      );
      await render();

      const [buttonA, buttonB] = await checkInButtons();
      await buttonA.click();
      await fixture.whenStable();
      expect(await buttonA.isDisabled()).toBe(true);
      expect(await buttonB.isDisabled()).toBe(false);

      await buttonB.click();
      await fixture.whenStable();
      expect(checkIn.mock.calls).toEqual([['f1'], ['f2']]);
      expect(await buttonB.isDisabled()).toBe(true);

      // La fine della prima richiesta non riattiva la seconda, ancora in corso.
      first.error(new HttpErrorResponse({ status: 500 }));
      await fixture.whenStable();
      expect(await buttonA.isDisabled()).toBe(false);
      expect(await buttonB.isDisabled()).toBe(true);
    });

    it('removes the check-in alerts when the list is reloaded', async () => {
      checkIn.mockReturnValue(
        throwError(() =>
          problem(422, 'Il check-in di questa prenotazione è già stato registrato.'),
        ),
      );
      getMine
        .mockReturnValueOnce(of(response()))
        .mockReturnValueOnce(
          of(response({ upcoming: [UPCOMING[0]], cancelled: [UPCOMING[1], ...CANCELLED] })),
        );
      await render();

      await clickCheckIn();
      expect(element.querySelectorAll('[role="alert"]').length).toBe(1);

      // Il ricaricamento dopo la cancellazione della «Sala studio B2».
      await (await cancelButtons())[1].click();
      await fixture.whenStable();
      await (
        await rootLoader.getHarness(MatButtonHarness.with({ text: 'Cancella prenotazione' }))
      ).click();
      await fixture.whenStable();

      expect(getMine).toHaveBeenCalledTimes(2);
      expect(titles()).toEqual(['Aula A1']);
      expect(element.querySelectorAll('[role="alert"]').length).toBe(0);
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

  it('closes the dialog and goes back to the login on 401 when cancelling', async () => {
    sessionStorage.setItem('smartcampus.session', JSON.stringify(SESSION));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: EmptyPage },
          { path: 'my-bookings', component: MyBookings },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/my-bookings');
    const loader = TestbedHarnessEnvironment.documentRootLoader(harness.fixture);

    // Qui l'ora del dispositivo è quella reale: prenotazione sintetica lontana nel futuro.
    const upcoming: MyReservationItem = {
      id: 'f1',
      roomId: 'r1',
      roomName: 'Aula A1',
      buildingName: 'Edificio A',
      start: '2099-11-05T09:00:00+00:00',
      end: '2099-11-05T10:00:00+00:00',
      isGroup: false,
      organizerName: null,
      isOrganizer: true,
      checkedInAt: null,
      members: [],
    };
    http.expectOne('/api/bookings/mine').flush({ upcoming: [upcoming], past: [], cancelled: [] });
    await harness.fixture.whenStable();

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Cancella' }))).click();
    await harness.fixture.whenStable();
    await (
      await loader.getHarness(MatButtonHarness.with({ text: 'Cancella prenotazione' }))
    ).click();
    await harness.fixture.whenStable();

    const request = http.expectOne('/api/bookings/f1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 401, statusText: 'Unauthorized' });
    await harness.fixture.whenStable();

    expect(await loader.hasHarness(MatDialogHarness)).toBe(false);
    expect(document.querySelector('[role="alert"]')).toBeNull();
    expect(await loader.hasHarness(MatSnackBarHarness)).toBe(false);
    expect(auth.isLoggedIn()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/login?reason=expired&returnUrl=%2Fmy-bookings');
    http.verify();
  });

  it('goes back to the login without alert on 401 when checking in', async () => {
    sessionStorage.setItem('smartcampus.session', JSON.stringify(SESSION));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: EmptyPage },
          { path: 'my-bookings', component: MyBookings },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/my-bookings');
    const loader = TestbedHarnessEnvironment.documentRootLoader(harness.fixture);

    // Qui l'ora del dispositivo è quella reale: prenotazione sintetica che inizia fra 5 minuti.
    const start = Date.now() + 5 * 60_000;
    const upcoming: MyReservationItem = {
      id: 'f1',
      roomId: 'r1',
      roomName: 'Aula A1',
      buildingName: 'Edificio A',
      start: new Date(start).toISOString(),
      end: new Date(start + 60 * 60_000).toISOString(),
      isGroup: false,
      organizerName: null,
      isOrganizer: true,
      checkedInAt: null,
      members: [],
    };
    http.expectOne('/api/bookings/mine').flush({ upcoming: [upcoming], past: [], cancelled: [] });
    await harness.fixture.whenStable();

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Check-in' }))).click();
    await harness.fixture.whenStable();

    const request = http.expectOne('/api/bookings/f1/check-in');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 401, statusText: 'Unauthorized' });
    await harness.fixture.whenStable();

    expect(document.querySelector('[role="alert"]')).toBeNull();
    expect(await loader.hasHarness(MatSnackBarHarness)).toBe(false);
    expect(auth.isLoggedIn()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/login?reason=expired&returnUrl=%2Fmy-bookings');
    http.verify();
  });
});
