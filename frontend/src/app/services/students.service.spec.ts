import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StudentSummary } from '../models/user.model';
import { StudentsService } from './students.service';

// Dati sintetici di prova.
const STUDENTS: StudentSummary[] = [
  { id: 's2', username: 'student2', displayName: 'Anna Bianchi' },
];

describe('StudentsService', () => {
  let service: StudentsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(StudentsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('searches the students with GET /api/identity/students and returns them', async () => {
    const result = new Promise((resolve) => service.search('bia').subscribe(resolve));

    const request = http.expectOne((req) => req.url === '/api/identity/students');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe('bia');
    expect(request.request.urlWithParams).toBe('/api/identity/students?search=bia');
    request.flush(STUDENTS);

    expect(await result).toEqual(STUDENTS);
  });

  it('encodes the search text in the query string', () => {
    service.search('Rossi & Co').subscribe();

    const request = http.expectOne((req) => req.url === '/api/identity/students');
    expect(request.request.params.get('search')).toBe('Rossi & Co');
    expect(request.request.urlWithParams).toBe('/api/identity/students?search=Rossi%20%26%20Co');
    request.flush([]);
  });
});
