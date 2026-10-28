import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatButtonHarness } from '@angular/material/button/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { Component } from '@angular/core';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { RoomDetail } from './room-detail';
import { AuthService } from '../../services/auth.service';
import { RoomsService } from '../../services/rooms.service';
import { BookingsService } from '../../services/bookings.service';
import { RoomDetail as RoomDetailData } from '../../models/room.model';
import { DailyAvailability } from '../../models/availability.model';

@Component({ template: '' })
class EmptyPage {}

// Dati sintetici per i test
const mockRoom: RoomDetailData = {
  id: 'room-1',
  name: 'Aula A1',
  type: 'Classroom',
  buildingId: 'b-1',
  buildingName: 'Edificio A',
  buildingCode: 'A',
  floor: 0,
  capacity: 30,
  isBookable: true,
  equipment: [{ id: 'e1', name: 'Proiettore' }],
  openingHours: [
    { dayOfWeek: 'Monday', opensAt: '08:00:00', closesAt: '20:00:00', isClosed: false },
    { dayOfWeek: 'Sunday', opensAt: null, closesAt: null, isClosed: true },
  ],
};

const mockAvailability: DailyAvailability = {
  roomId: 'room-1',
  date: '2026-11-03',
  isOpen: true,
  slots: [
    { start: '2026-11-03T08:00:00+01:00', end: '2026-11-03T08:30:00+01:00', isFree: true },
    { start: '2026-11-03T08:30:00+01:00', end: '2026-11-03T09:00:00+01:00', isFree: false },
  ],
};

const mockClosedAvailability: DailyAvailability = {
  roomId: 'room-1',
  date: '2026-11-08',
  isOpen: false,
  slots: [],
};

describe('RoomDetail', () => {
  let component: RoomDetail;
  let fixture: ComponentFixture<RoomDetail>;
  let mockRoomsService: { getRoomById: ReturnType<typeof vi.fn>; deactivate: ReturnType<typeof vi.fn> };
  let mockBookingsService: { getDailyAvailability: ReturnType<typeof vi.fn> };
  let mockDialog: { open: ReturnType<typeof vi.fn>; afterClosedResult: unknown };
  let currentParamId: string | null = 'room-1';
  const isStaff = signal(false);
  const isLoggedIn = signal(true);

  beforeEach(async () => {
    currentParamId = 'room-1';
    isStaff.set(false);
    isLoggedIn.set(true);
    mockRoomsService = {
      getRoomById: vi.fn().mockReturnValue(of(mockRoom)),
      deactivate: vi.fn().mockReturnValue(of(undefined)),
    };
    mockDialog = {
      afterClosedResult: true,
      open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(mockDialog.afterClosedResult) })),
    };
    mockBookingsService = {
      getDailyAvailability: vi.fn().mockReturnValue(of(mockAvailability)),
    };

    await TestBed.configureTestingModule({
      imports: [RoomDetail],
      providers: [
        provideRouter([{ path: 'rooms/:id/book', component: EmptyPage }]),
        { provide: RoomsService, useValue: mockRoomsService },
        { provide: AuthService, useValue: { isStaff, isLoggedIn } },
        { provide: BookingsService, useValue: mockBookingsService },
        { provide: MatDialog, useValue: mockDialog },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? currentParamId : null),
              },
            },
          },
        },
      ],
    }).compileComponents();
  });

  // Test 1: Creazione base del componente
  it('dovrebbe creare il componente', () => {
    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component).toBeTruthy();
  });

  // Test 2: Criterio US-02 - Visualizzazione di tutti i dati dello spazio
  it('dovrebbe mostrare tutti i dati dello spazio quando caricato con successo', () => {
    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica nome, edificio, tipo, capienza
    expect(compiled.textContent).toContain('Aula A1');
    expect(compiled.textContent).toContain('Edificio A');
    expect(compiled.textContent).toContain('Aula');
    expect(compiled.textContent).toContain('30 posti');

    // Verifica attrezzature
    expect(compiled.textContent).toContain('Proiettore');

    // Verifica orari settimanali con traduzione corretta dei giorni
    expect(compiled.textContent).toContain('Luned+¼');
    expect(compiled.textContent).toContain('08:00 ÔÇô 20:00');
    expect(compiled.textContent).toContain('Domenica');
    expect(compiled.textContent).toContain('Chiuso');

    // Verifica indicazione di prenotabilit+á
    expect(compiled.textContent).toContain('Prenotabile');
  });

  // Test 3: Criterio US-02 - Indicazione chiara se lo spazio non +¿ prenotabile
  it('dovrebbe indicare chiaramente se lo spazio non +¿ prenotabile', () => {
    const nonBookableRoom: RoomDetailData = {
      ...mockRoom,
      isBookable: false,
    };
    mockRoomsService.getRoomById.mockReturnValue(of(nonBookableRoom));

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica badge "Non prenotabile"
    expect(compiled.textContent).toContain('Non prenotabile');

    // Verifica che il pulsante sia disabilitato
    const disabledButtons = compiled.querySelectorAll('button[disabled]');
    expect(disabledButtons.length).toBeGreaterThan(0);
    const hasNonBookableDisabled = Array.from(disabledButtons).some((btn) =>
      btn.textContent?.includes('Non prenotabile'),
    );
    expect(hasNonBookableDisabled).toBe(true);
  });

  // Test 4: Criterio US-02 - Messaggio esplicito "spazio non trovato" su errore 404
  it('dovrebbe mostrare "Spazio non trovato" quando il server risponde 404', () => {
    mockRoomsService.getRoomById.mockReturnValue(
      throwError(() => ({ status: 404 }))
    );

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica messaggio esplicito richiesto dalla storia
    expect(compiled.textContent).toContain('Spazio non trovato');
    expect(compiled.textContent).toContain('Lo spazio cercato non esiste o non +¿ pi+¦ attivo');

    // Verifica presenza del pulsante per tornare all'elenco
    const backLink = compiled.querySelector('a[routerLink="/rooms"]');
    expect(backLink).toBeTruthy();
  });

  // Test 5: Spazio non trovato se l'URL non contiene alcun ID
  it('dovrebbe mostrare "Spazio non trovato" se il parametro id +¿ assente', () => {
    currentParamId = null;

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Spazio non trovato');
  });

  // Test 6: Mostra messaggio di errore generico se la richiesta fallisce con errore diverso da 404
  it('dovrebbe mostrare il messaggio di errore generico se la richiesta fallisce con errore di rete', () => {
    mockRoomsService.getRoomById.mockReturnValue(
      throwError(() => ({ status: 500 }))
    );

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Impossibile caricare lo spazio');
    expect(compiled.textContent).toContain(
      'Si +¿ verificato un errore durante il caricamento dei dettagli dello spazio.'
    );
  });

  // Il personale pu+¦ aprire il modulo di modifica, gli altri no.
  it('dovrebbe mostrare il pulsante Modifica solo al personale', () => {
    isStaff.set(true);

    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>(
      'a[href="/staff/rooms/room-1/edit"]',
    );
    expect(link?.textContent).toContain('Modifica');
  });

  it('dovrebbe nascondere il pulsante Modifica a chi non +¿ del personale', () => {
    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('a[href="/staff/rooms/room-1/edit"]')).toBeNull();
  });

  // US-04: -½Prenota spazio-+ apre la pagina di prenotazione dello spazio prenotabile.
  it('dovrebbe collegare -½Prenota spazio-+ alla pagina di prenotazione dello spazio', async () => {
    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();
    const loader = TestbedHarnessEnvironment.loader(fixture);

    const bookButton = await loader.getHarness(MatButtonHarness.with({ text: /Prenota spazio/ }));
    expect(await bookButton.isDisabled()).toBe(false);
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>(
      'a[href="/rooms/room-1/book"]',
    );
    expect(link?.textContent).toContain('Prenota spazio');

    await bookButton.click();
    await fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/rooms/room-1/book');
  });

  // US-04: sugli spazi non prenotabili resta solo il pulsante disabilitato -½Non prenotabile-+.
  it('non dovrebbe mostrare -½Prenota spazio-+ attivo su uno spazio non prenotabile', async () => {
    mockRoomsService.getRoomById.mockReturnValue(of({ ...mockRoom, isBookable: false }));
    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();
    const loader = TestbedHarnessEnvironment.loader(fixture);

    const notBookable = await loader.getHarness(MatButtonHarness.with({ text: /Non prenotabile/ }));
    expect(await notBookable.isDisabled()).toBe(true);
    expect(
      await loader.getHarnessOrNull(MatButtonHarness.with({ text: /Prenota spazio/ })),
    ).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('a[href$="/book"]')).toBeNull();
  });

  // Test 7: Criteri 2, 3 e 4 - Fasce libere, occupate e privacy
  it('dovrebbe mostrare le fasce libere e occupate senza esporre chi ha prenotato', () => {
    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica orari formattati
    expect(compiled.textContent).toContain('08:00 ÔÇô 08:30');
    expect(compiled.textContent).toContain('08:30 ÔÇô 09:00');

    // Verifica etichette degli stati
    expect(compiled.textContent).toContain('Libera');
    expect(compiled.textContent).toContain('Occupata');

    // Verifica privacy: nessun nome o campo utente presente
    expect(compiled.textContent).not.toContain('organizer');
    expect(compiled.textContent).not.toContain('Mario Rossi');
  });

  // Test 8: Criterio 5 - Giorno di chiusura
  it('dovrebbe mostrare "Spazio chiuso" nei giorni in cui lo spazio non +¿ aperto', () => {
    mockBookingsService.getDailyAvailability.mockReturnValue(of(mockClosedAvailability));

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Spazio chiuso');
    expect(compiled.textContent).not.toContain('08:00 ÔÇô 08:30');
  });

  // Test 9: Cambio data da selettore
  it('dovrebbe ricaricare la disponibilit+á quando viene selezionata una nuova data', () => {
    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    // Simula la selezione di una nuova data dal Datepicker
    component['onDateChange'](new Date(2026, 10, 5)); // 5 novembre 2026
    fixture.detectChanges();

    expect(mockBookingsService.getDailyAvailability).toHaveBeenCalledWith('room-1', '2026-11-05');
  });

  // Test 10: Visitatore senza accesso (non autenticato)
  it('dovrebbe invitare ad accedere senza chiamare il servizio di disponibilit+á se l\'utente non +¿ autenticato', () => {
    isLoggedIn.set(false);

    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica messaggio di invito al login
    expect(compiled.textContent).toContain('Accedi per vedere la disponibilit+á');
    expect(compiled.textContent).toContain(
      'La consultazione delle fasce orarie +¿ riservata agli utenti autenticati del campus',
    );

    // Verifica presenza del link al login con returnUrl
    const loginLink = compiled.querySelector<HTMLAnchorElement>('a[href^="/login"]');
    expect(loginLink).toBeTruthy();

    // Verifica che il servizio di disponibilit+á non sia stato chiamato
    expect(mockBookingsService.getDailyAvailability).not.toHaveBeenCalled();
  });
  // Pulsante Dismetti: visibilità solo al personale
  it('dovrebbe mostrare il pulsante Dismetti solo al personale', () => {
    isStaff.set(true);

    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const button = Array.from(compiled.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Dismetti'),
    );
    expect(button).toBeTruthy();
  });

  it('dovrebbe nascondere il pulsante Dismetti a chi non è del personale', () => {
    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const button = Array.from(compiled.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Dismetti'),
    );
    expect(button).toBeUndefined();
  });

  // Flusso di dismissione
  it('dovrebbe aprire la finestra di conferma e, se confermata, dismettere lo spazio e tornare all''elenco', () => {
    isStaff.set(true);

    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));

    const compiled = fixture.nativeElement as HTMLElement;
    const button = Array.from(compiled.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Dismetti'),
    ) as HTMLButtonElement;
    button.click();

    expect(mockDialog.open).toHaveBeenCalledTimes(1);
    const data = mockDialog.open.mock.calls[0][1].data;
    expect(data).toEqual({ roomId: 'room-1', roomName: 'Aula A1' });

    expect(mockRoomsService.deactivate).toHaveBeenCalledWith('room-1');
    expect(vi.mocked(router.navigate)).toHaveBeenCalledWith(['/rooms']);
  });

  it('dovrebbe non dismettere lo spazio se la finestra viene annullata', () => {
    isStaff.set(true);
    mockDialog.afterClosedResult = false;

    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));

    const compiled = fixture.nativeElement as HTMLElement;
    const button = Array.from(compiled.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Dismetti'),
    ) as HTMLButtonElement;
    button.click();

    expect(mockDialog.open).toHaveBeenCalledTimes(1);
    expect(mockRoomsService.deactivate).not.toHaveBeenCalled();
    expect(vi.mocked(router.navigate)).not.toHaveBeenCalled();
  });

  it('dovrebbe mostrare un messaggio inline se la dismissione fallisce', () => {
    isStaff.set(true);
    mockRoomsService.deactivate.mockReturnValue(throwError(() => ({ status: 500 })));

    fixture = TestBed.createComponent(RoomDetail);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockImplementation(() => Promise.resolve(true));

    const compiled = fixture.nativeElement as HTMLElement;
    const button = Array.from(compiled.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Dismetti'),
    ) as HTMLButtonElement;
    button.click();

    fixture.detectChanges();
    expect(compiled.textContent).toContain(
      'Si è verificato un errore durante la dismissione dello spazio. Riprova.',
    );
    expect(vi.mocked(router.navigate)).not.toHaveBeenCalled();
  });});