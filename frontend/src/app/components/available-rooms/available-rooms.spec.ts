import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { AvailableRoomResponse } from '../../models/availability.model';
import { toIsoDate } from '../../models/booking.model';
import { BookingsService } from '../../services/bookings.service';
import { AvailableRooms } from './available-rooms';

const ROOM_A: AvailableRoomResponse = {
  roomId: 'r1',
  name: 'Aula A1',
  buildingName: 'Edificio A',
  type: 'Classroom',
  capacity: 30,
};

const ROOM_B: AvailableRoomResponse = {
  roomId: 'r2',
  name: 'Laboratorio Turing',
  buildingName: 'Edificio B',
  type: 'Laboratory',
  capacity: 20,
};

describe('AvailableRooms', () => {
  const bookingsServiceMock = {
    searchAvailableRooms: vi.fn(),
  };

  let fixture: ComponentFixture<AvailableRooms>;
  let component: AvailableRooms;
  let element: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AvailableRooms],
      providers: [
        provideRouter([]),
        { provide: BookingsService, useValue: bookingsServiceMock },
      ],
    });

    fixture = TestBed.createComponent(AvailableRooms);
    component = fixture.componentInstance;
    element = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('inizializza il modulo con data odierna e orario di default', () => {
    expect(component['form'].controls.date.value).toBeTruthy();
    expect(component['form'].controls.start.value).toBeTruthy();
    expect(component['form'].controls.end.value).toBeTruthy();
    expect(component['form'].controls.end.value! > component['form'].controls.start.value!).toBe(true);
  });

  it('cerca gli spazi liberi e mostra i risultati con collegamento alla prenotazione già compilata', () => {
    component['form'].controls.start.setValue('09:00');
    component['form'].controls.end.setValue('10:00');
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(of([ROOM_A, ROOM_B]));

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    expect(bookingsServiceMock.searchAvailableRooms).toHaveBeenCalledWith({
      date: expect.any(String),
      start: '09:00',
      end: '10:00',
      type: null,
      minCapacity: null,
    });

    const cards = element.querySelectorAll('.room-card');
    expect(cards.length).toBe(2);

    expect(cards[0].textContent).toContain('Aula A1');
    expect(cards[0].textContent).toContain('Edificio A');
    expect(cards[0].textContent).toContain('Aula');
    expect(cards[0].textContent).toContain('30 posti');

    // Il pulsante prenota punta a /rooms/r1/book con i queryParams valorizzati
    const bookLink = cards[0].querySelector<HTMLAnchorElement>('a')!;
    expect(bookLink.getAttribute('href')).toContain('/rooms/r1/book');
    expect(bookLink.getAttribute('href')).toContain('start=09:00');
    expect(bookLink.getAttribute('href')).toContain('end=10:00');
  });

  it('mostra un messaggio esplicito se nessuno spazio è libero', () => {
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(of([]));

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    const emptyState = element.querySelector('.empty-state');
    expect(emptyState).toBeTruthy();
    expect(emptyState!.textContent).toContain('Nessuno spazio è libero nella fascia oraria selezionata.');
  });

  it('mostra un messaggio di errore se la ricerca fallisce', () => {
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(throwError(() => new Error('Network error')));

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    const errorMessage = element.querySelector('.error-message');
    expect(errorMessage).toBeTruthy();
    expect(errorMessage!.textContent).toContain('Non è stato possibile caricare gli spazi liberi.');
  });

  it('aggiorna l\'ora di fine quando l\'ora di inizio scelta è uguale o successiva', () => {
    component['form'].controls.start.setValue('10:00');
    component['form'].controls.end.setValue('10:00');

    // Quando start diventa 11:00, end deve aggiornarsi al primo slot successivo (11:30)
    component['form'].controls.start.setValue('11:00');
    fixture.detectChanges();

    expect(component['form'].controls.end.value).toBe('11:30');
  });

  it('invia i filtri opzionali per tipo e capienza minima', () => {
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(of([]));

    component['form'].patchValue({
      start: '09:00',
      end: '10:00',
      type: 'Laboratory',
      minCapacity: 25,
    });

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    expect(bookingsServiceMock.searchAvailableRooms).toHaveBeenCalledWith({
      date: expect.any(String),
      start: '09:00',
      end: '10:00',
      type: 'Laboratory',
      minCapacity: 25,
    });
  });

  it('mostra un errore e non chiama il servizio se l\'ora di fine è precedente o uguale all\'inizio', () => {
    // Forziamo un valore errato nel form
    component['form'].controls.start.setValue('14:00', { emitEvent: false });
    component['form'].controls.end.setValue('10:00', { emitEvent: false });

    component['search']();
    fixture.detectChanges();

    expect(bookingsServiceMock.searchAvailableRooms).not.toHaveBeenCalled();
    expect(element.querySelector('.error-message')!.textContent).toContain(
      'L\'ora di fine deve essere successiva a quella di inizio.',
    );
  });

  it('mostra valori di riserva quando edificio e tipo non sono specificati', () => {
    const uncompleteRoom: AvailableRoomResponse = {
      roomId: 'r3',
      name: 'Stanza Libera',
      buildingName: null,
      type: null,
      capacity: 5,
    };
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(of([uncompleteRoom]));

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    const card = element.querySelector('.room-card')!;
    expect(card.textContent).toContain('Stanza Libera');
    expect(card.textContent).toContain('Edificio non specificato');
    expect(card.textContent).toContain('Spazio');
    expect(card.textContent).toContain('5 posti');
  });

  it('aggiorna il link Prenota con la data e gli orari cercati per un giorno futuro', () => {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const expectedDate = toIsoDate(tomorrow);

    component['form'].controls.date.setValue(tomorrow);
    component['form'].controls.start.setValue('14:00');
    component['form'].controls.end.setValue('16:00');
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(of([ROOM_A]));

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    const bookLink = element.querySelector<HTMLAnchorElement>('.room-card a')!;
    expect(bookLink.getAttribute('href')).toContain(`date=${expectedDate}`);
    expect(bookLink.getAttribute('href')).toContain('start=14:00');
    expect(bookLink.getAttribute('href')).toContain('end=16:00');
  });

  it('preserva nel link i valori cercati anche se i controlli del form vengono modificati successivamente', () => {
    component['form'].controls.start.setValue('10:00');
    component['form'].controls.end.setValue('11:00');
    bookingsServiceMock.searchAvailableRooms.mockReturnValue(of([ROOM_A]));

    const submitButton = element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submitButton.click();
    fixture.detectChanges();

    // L'utente modifica l'orario nel form dopo aver cercato
    component['form'].controls.start.setValue('15:00');
    component['form'].controls.end.setValue('16:00');
    fixture.detectChanges();

    // Il link della card deve preservare la fascia cercata (10:00 - 11:00), non quella non cercata
    const bookLink = element.querySelector<HTMLAnchorElement>('.room-card a')!;
    expect(bookLink.getAttribute('href')).toContain('start=10:00');
    expect(bookLink.getAttribute('href')).toContain('end=11:00');
  });

  it('aggiorna reattivamente le opzioni di fine quando cambia l inizio', () => {
    component['form'].controls.start.setValue('08:00');
    fixture.detectChanges();
    expect(component['endOptions']()).toContain('08:30');

    component['form'].controls.start.setValue('14:00');
    fixture.detectChanges();
    expect(component['endOptions']()).not.toContain('08:30');
    expect(component['endOptions']()).toContain('14:30');
  });
});
