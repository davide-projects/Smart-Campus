import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RoomsService } from './rooms.service';

describe('RoomsService', () => {
  let service: RoomsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RoomsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('senza filtri chiama /api/rooms senza parametri', () => {
    service.search().subscribe();

    const request = http.expectOne('/api/rooms');
    expect(request.request.params.keys()).toEqual([]);
    request.flush([]);
  });

  it('invia solo i filtri valorizzati', () => {
    service.search({ type: 'StudyRoom', minCapacity: 30, buildingId: null }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/rooms');
    expect(request.request.params.get('type')).toBe('StudyRoom');
    expect(request.request.params.get('minCapacity')).toBe('30');
    expect(request.request.params.has('buildingId')).toBe(false);
    request.flush([]);
  });
});
