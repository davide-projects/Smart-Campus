import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RoomSummary, RoomWriteRequest } from '../models/room.model';
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

  it('calls /api/rooms without parameters when there are no filters', () => {
    service.search().subscribe();

    const request = http.expectOne('/api/rooms');
    expect(request.request.params.keys()).toEqual([]);
    request.flush([]);
  });

  it('sends only the filters that have a value', () => {
    service.search({ type: 'StudyRoom', minCapacity: 30, buildingId: null }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/rooms');
    expect(request.request.params.get('type')).toBe('StudyRoom');
    expect(request.request.params.get('minCapacity')).toBe('30');
    expect(request.request.params.has('buildingId')).toBe(false);
    request.flush([]);
  });

  const room = (id: string, buildingId: string, buildingName: string): RoomSummary => ({
    id,
    name: `Spazio ${id}`,
    type: 'Classroom',
    capacity: 20,
    floor: 0,
    isBookable: true,
    buildingId,
    buildingName,
  });

  it('lists each building once, in alphabetical order', () => {
    let result: { id: string; name: string }[] = [];
    service.listBuildings().subscribe((buildings) => (result = buildings));

    http
      .expectOne('/api/rooms')
      .flush([room('1', 'b-2', 'Edificio B'), room('2', 'b-1', 'Edificio A'), room('3', 'b-2', 'Edificio B')]);

    expect(result).toEqual([
      { id: 'b-1', name: 'Edificio A' },
      { id: 'b-2', name: 'Edificio B' },
    ]);
  });

  const writeRequest: RoomWriteRequest = {
    buildingId: 'b-1',
    name: 'Aula C1',
    type: 'Classroom',
    capacity: 30,
    floor: 1,
    isBookable: true,
    equipmentIds: [],
    openingHours: [{ dayOfWeek: 'Monday', opensAt: '08:00:00', closesAt: '20:00:00' }],
  };

  it('creates a room with a POST and returns its id', () => {
    let id = '';
    service.create(writeRequest).subscribe((created) => (id = created.id));

    const request = http.expectOne('/api/rooms');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(writeRequest);
    request.flush({ id: 'new-room' });

    expect(id).toBe('new-room');
  });

  it('updates a room with a PUT on its id', () => {
    service.update('room-1', writeRequest).subscribe();

    const request = http.expectOne('/api/rooms/room-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(writeRequest);
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
