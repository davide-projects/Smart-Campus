import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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
});
