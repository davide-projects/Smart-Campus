import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DailyAvailability } from '../models/availability.model';
import { MyReservationsResponse } from '../models/booking.model';
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
});
