import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { ReservationListItem } from '../../models/booking.model';
import { RoomSummary } from '../../models/room.model';
import { BookingsService } from '../../services/bookings.service';
import { RoomsService } from '../../services/rooms.service';
import { RoomReservations } from './room-reservations';

const ROOM_A: RoomSummary = {
  id: 'room-a',
  name: 'Aula A1',
  type: 'Classroom',
  capacity: 30,
  floor: 1,
  isBookable: true,
  buildingId: 'b-1',
  buildingName: 'Edificio A',
};

const ROOM_B: RoomSummary = {
  id: 'room-b',
  name: 'Laboratorio Turing',
  type: 'Laboratory',
  capacity: 20,
  floor: 2,
  isBookable: true,
  buildingId: 'b-2',
  buildingName: 'Edificio B',
};

const RESERVATIONS: ReservationListItem[] = [
  {
    id: 'res-1',
    start: '2026-11-05T09:00:00+01:00',
    end: '2026-11-05T10:00:00+01:00',
    organizerId: '11111111-1111-1111-1111-111111111111',
    participants: 1,
    memberIds: [],
    status: 'Confirmed',
    cancelledAt: null,
  },
  {
    id: 'res-2',
    start: '2026-11-05T11:00:00+01:00',
    end: '2026-11-05T12:30:00+01:00',
    organizerId: '22222222-2222-2222-2222-222222222222',
    participants: 4,
    memberIds: ['33333333-3333-3333-3333-333333333333'],
    status: 'Cancelled',
    cancelledAt: '2026-11-04T15:00:00+01:00',
  },
];

describe('RoomReservations', () => {
  const roomsServiceMock = {
    search: vi.fn(),
  };

  const bookingsServiceMock = {
    getReservations: vi.fn(),
  };

  let fixture: ComponentFixture<RoomReservations>;
  let component: RoomReservations;
  let element: HTMLElement;

  function setup(queryRoomId: string | null = null): void {
    TestBed.configureTestingModule({
      imports: [RoomReservations],
      providers: [
        provideRouter([]),
        { provide: RoomsService, useValue: roomsServiceMock },
        { provide: BookingsService, useValue: bookingsServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (key: string) => (key === 'roomId' ? queryRoomId : null),
              },
            },
          },
        },
      ],
    });

    fixture = TestBed.createComponent(RoomReservations);
    component = fixture.componentInstance;
    element = fixture.nativeElement;
  }

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('carica gli spazi all\'avvio e invita a selezionare uno spazio se non scelto', () => {
    roomsServiceMock.search.mockReturnValue(of([ROOM_A, ROOM_B]));
    setup();
    fixture.detectChanges();

    expect(roomsServiceMock.search).toHaveBeenCalled();
    expect(bookingsServiceMock.getReservations).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Seleziona uno spazio per visualizzare le relative prenotazioni.');
  });

  it('pre-seleziona lo spazio e carica le prenotazioni se presente queryParam roomId', () => {
    roomsServiceMock.search.mockReturnValue(of([ROOM_A, ROOM_B]));
    bookingsServiceMock.getReservations.mockReturnValue(of(RESERVATIONS));
    setup('room-a');
    fixture.detectChanges();

    expect(bookingsServiceMock.getReservations).toHaveBeenCalledWith(
      'room-a',
      expect.any(String),
    );
    expect(element.querySelector('.reservations-table')).toBeTruthy();
  });

  it('mostra correttamente le prenotazioni nella tabella con orari, badge di stato, organizzatore e partecipanti', () => {
    roomsServiceMock.search.mockReturnValue(of([ROOM_A]));
    bookingsServiceMock.getReservations.mockReturnValue(of(RESERVATIONS));
    setup('room-a');
    fixture.detectChanges();

    const rows = element.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    // Prima riga: Confermata
    expect(rows[0].textContent).toContain('09:00 – 10:00');
    expect(rows[0].textContent).toContain('Confermata');
    expect(rows[0].textContent).toContain('11111111-1111-1111-1111-111111111111');
    expect(rows[0].textContent).toContain('1 persona');
    expect(rows[0].querySelector('.status-badge.confirmed')).toBeTruthy();

    // Seconda riga: Cancellata
    expect(rows[1].textContent).toContain('11:00 – 12:30');
    expect(rows[1].textContent).toContain('Cancellata');
    expect(rows[1].textContent).toContain('22222222-2222-2222-2222-222222222222');
    expect(rows[1].textContent).toContain('4 persone');
    expect(rows[1].querySelector('.status-badge.cancelled')).toBeTruthy();
  });

  it('mostra il messaggio di stato vuoto quando non ci sono prenotazioni', () => {
    roomsServiceMock.search.mockReturnValue(of([ROOM_A]));
    bookingsServiceMock.getReservations.mockReturnValue(of([]));
    setup('room-a');
    fixture.detectChanges();

    expect(element.querySelector('.empty-state')).toBeTruthy();
    expect(element.textContent).toContain(
      'Nessuna prenotazione presente per questo spazio nel giorno selezionato.',
    );
  });

  it('mostra un messaggio di errore se la chiamata alle prenotazioni fallisce', () => {
    roomsServiceMock.search.mockReturnValue(of([ROOM_A]));
    bookingsServiceMock.getReservations.mockReturnValue(throwError(() => new Error('Network error')));
    setup('room-a');
    fixture.detectChanges();

    expect(element.querySelector('.error-message')).toBeTruthy();
    expect(element.textContent).toContain(
      'Non è stato possibile caricare le prenotazioni. Riprova più tardi.',
    );
  });

  it('mostra un messaggio di errore se il caricamento degli spazi fallisce', () => {
    roomsServiceMock.search.mockReturnValue(throwError(() => new Error('Server error')));
    setup();
    fixture.detectChanges();

    expect(element.querySelector('.error-message')).toBeTruthy();
    expect(element.textContent).toContain(
      'Non è stato possibile caricare l\'elenco degli spazi.',
    );
  });
});
