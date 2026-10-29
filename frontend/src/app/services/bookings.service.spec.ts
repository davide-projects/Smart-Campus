import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DailyAvailability, AvailableRoomResponse } from '../models/availability.model';
import {
  CreateReservationRequest,
  MyReservationsResponse,
  ReservationListItem,
  ReservationResponse,
} from '../models/booking.model';
import { BookingsService } from './bookings.service';

// Dati sintetici di prova.
const RESPONSE: MyReservationsResponse = {
  upcoming: [
    {
      id: 'p1',
      roomId: 'r1',
      roomName: 'Aula A1',
      buildingName: 'Edificio A',
      start: '2026-11-05T09:00:00+00:00',
      end: '2026-11-05T10:00:00+00:00',
      isGroup: false,
      organizerName: 'Mario Rossi',
      isOrganizer: true,
    },
  ],
  past: [],
  cancelled: [],
};

describe('BookingsService', () => {
  let service: BookingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(BookingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the reservations of the current user from /api/bookings/mine', async () => {
    const result = new Promise((resolve) => service.getMine().subscribe(resolve));

    const request = http.expectOne('/api/bookings/mine');
    expect(request.request.method).toBe('GET');
    request.flush(RESPONSE);

    expect(await result).toEqual(RESPONSE);
  });

  it('richiede la disponibilità giornaliera inviando roomId e date come parametri di query', () => {
    const mockResponse: DailyAvailability = {
      roomId: '11111111-1111-1111-1111-111111111111',
      date: '2026-11-03',
      isOpen: true,
      slots: [
        { start: '2026-11-03T08:00:00+01:00', end: '2026-11-03T08:30:00+01:00', isFree: true },
        { start: '2026-11-03T08:30:00+01:00', end: '2026-11-03T09:00:00+01:00', isFree: false },
      ],
    };

    let result: DailyAvailability | undefined;
    service.getDailyAvailability('11111111-1111-1111-1111-111111111111', '2026-11-03').subscribe((data) => {
      result = data;
    });

    const request = http.expectOne(
      (req) => req.url === '/api/bookings/availability' &&
        req.params.get('roomId') === '11111111-1111-1111-1111-111111111111' &&
        req.params.get('date') === '2026-11-03'
    );
    expect(request.request.method).toBe('GET');

    request.flush(mockResponse);

    expect(result).toEqual(mockResponse);
  });

  it('restituisce correttamente la disponibilità quando lo spazio è chiuso', () => {
    const mockClosedResponse: DailyAvailability = {
      roomId: '11111111-1111-1111-1111-111111111111',
      date: '2026-11-08',
      isOpen: false,
      slots: [],
    };

    let result: DailyAvailability | undefined;
    service.getDailyAvailability('11111111-1111-1111-1111-111111111111', '2026-11-08').subscribe((data) => {
      result = data;
    });

    const request = http.expectOne((req) => req.url === '/api/bookings/availability');
    request.flush(mockClosedResponse);

    expect(result?.isOpen).toBe(false);
    expect(result?.slots).toEqual([]);
  });

  it('cancels a reservation with DELETE /api/bookings/{id}', async () => {
    const result = new Promise<void>((resolve) => service.cancel('p1').subscribe(() => resolve()));

    const request = http.expectOne('/api/bookings/p1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await result;
  });

  it('creates a reservation with POST /api/bookings and returns the confirmation', async () => {
    const body: CreateReservationRequest = {
      roomId: 'r1',
      start: '2026-11-05T10:00:00+01:00',
      end: '2026-11-05T11:00:00+01:00',
      purpose: 'Ripasso di analisi',
    };
    const created: ReservationResponse = {
      id: 'p9',
      roomId: 'r1',
      roomName: 'Aula A1',
      start: '2026-11-05T10:00:00+01:00',
      end: '2026-11-05T11:00:00+01:00',
      participants: 1,
      status: 'Confirmed',
    };
    const result = new Promise((resolve) => service.create(body).subscribe(resolve));

    const request = http.expectOne('/api/bookings');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(created, { status: 201, statusText: 'Created' });

    expect(await result).toEqual(created);
  });

  it('reads the count of future reservations of a room from the API response', async () => {
    const result = new Promise((resolve) => service.futureReservationsCount('room-1').subscribe(resolve));

    const request = http.expectOne('/api/bookings/rooms/room-1/future-reservations-count');
    expect(request.request.method).toBe('GET');
    request.flush({ roomId: 'room-1', count: 7 });

    expect(await result).toBe(7);
  });

  it('cerca gli spazi liberi per fascia oraria inviando i parametri di query', async () => {
    const mockResults: AvailableRoomResponse[] = [
      {
        roomId: 'room-1',
        name: 'Aula A1',
        buildingName: 'Edificio A',
        type: 'Classroom',
        capacity: 30,
      },
    ];

    const result = new Promise((resolve) =>
      service
        .searchAvailableRooms({
          date: '2026-11-04',
          start: '10:00',
          end: '12:00',
          type: 'Classroom',
          minCapacity: 20,
        })
        .subscribe(resolve),
    );

    const request = http.expectOne(
      (req) =>
        req.url === '/api/bookings/availability/search' &&
        req.params.get('date') === '2026-11-04' &&
        req.params.get('start') === '10:00' &&
        req.params.get('end') === '12:00' &&
        req.params.get('type') === 'Classroom' &&
        req.params.get('minCapacity') === '20',
    );
    expect(request.request.method).toBe('GET');
    request.flush(mockResults);

    expect(await result).toEqual(mockResults);
  });

  it('recupera le prenotazioni di uno spazio inviando roomId e date come parametri di query', async () => {
    const list: ReservationListItem[] = [
      {
        id: 'res-1',
        start: '2026-11-05T09:00:00+01:00',
        end: '2026-11-05T10:00:00+01:00',
        organizerId: 'user-1',
        participants: 3,
        memberIds: ['user-2', 'user-3'],
        status: 'Confirmed',
        cancelledAt: null,
      },
      {
        id: 'res-2',
        start: '2026-11-05T11:00:00+01:00',
        end: '2026-11-05T12:00:00+01:00',
        organizerId: 'user-4',
        participants: 1,
        memberIds: [],
        status: 'Cancelled',
        cancelledAt: '2026-11-04T18:00:00+01:00',
      },
    ];

    const result = new Promise((resolve) =>
      service.getReservations('room-123', '2026-11-05').subscribe(resolve),
    );

    const request = http.expectOne(
      (req) =>
        req.url === '/api/bookings' &&
        req.params.get('roomId') === 'room-123' &&
        req.params.get('date') === '2026-11-05',
    );
    expect(request.request.method).toBe('GET');
    request.flush(list);

    expect(await result).toEqual(list);
  });
});
