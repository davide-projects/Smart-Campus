import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { RoomDetail } from './room-detail';
import { RoomService } from '../../../services/room.service';
import { RoomDetail as RoomDetailData } from '../../../models/room.model';

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
    { dayOfWeek: 1, opensAt: '08:00:00', closesAt: '20:00:00', isClosed: false },
    { dayOfWeek: 0, opensAt: null, closesAt: null, isClosed: true },
  ],
};

describe('RoomDetail', () => {
  let component: RoomDetail;
  let fixture: ComponentFixture<RoomDetail>;
  let mockRoomService: { getRoomById: ReturnType<typeof vi.fn> };
  let currentParamId: string | null = 'room-1';

  beforeEach(async () => {
    currentParamId = 'room-1';
    mockRoomService = {
      getRoomById: vi.fn().mockReturnValue(of(mockRoom)),
    };

    await TestBed.configureTestingModule({
      imports: [RoomDetail],
      providers: [
        provideRouter([]),
        { provide: RoomService, useValue: mockRoomService },
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

    // Verifica orari settimanali
    expect(compiled.textContent).toContain('Lunedì');
    expect(compiled.textContent).toContain('08:00 – 20:00');
    expect(compiled.textContent).toContain('Domenica');
    expect(compiled.textContent).toContain('Chiuso');

    // Verifica indicazione di prenotabilità
    expect(compiled.textContent).toContain('Prenotabile');
  });

  // Test 3: Criterio US-02 - Indicazione chiara se lo spazio non è prenotabile
  it('dovrebbe indicare chiaramente se lo spazio non è prenotabile', () => {
    const nonBookableRoom: RoomDetailData = {
      ...mockRoom,
      isBookable: false,
    };
    mockRoomService.getRoomById.mockReturnValue(of(nonBookableRoom));

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica badge "Non prenotabile"
    expect(compiled.textContent).toContain('Non prenotabile');

    // Verifica che il pulsante sia disabilitato
    const disabledButton = compiled.querySelector('button[disabled]');
    expect(disabledButton).toBeTruthy();
    expect(disabledButton?.textContent).toContain('Non prenotabile');
  });

  // Test 4: Criterio US-02 - Messaggio esplicito "spazio non trovato" su errore 404
  it('dovrebbe mostrare "Spazio non trovato" quando il server risponde 404', () => {
    mockRoomService.getRoomById.mockReturnValue(
      throwError(() => ({ status: 404 }))
    );

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    // Verifica messaggio esplicito richiesto dalla storia
    expect(compiled.textContent).toContain('Spazio non trovato');
    expect(compiled.textContent).toContain('Lo spazio cercato non esiste o non è più attivo');

    // Verifica presenza del pulsante per tornare all'elenco
    const backLink = compiled.querySelector('a[routerLink="/rooms"]');
    expect(backLink).toBeTruthy();
  });

  // Test 5: Spazio non trovato se l'URL non contiene alcun ID
  it('dovrebbe mostrare "Spazio non trovato" se il parametro id è assente', () => {
    currentParamId = null;

    fixture = TestBed.createComponent(RoomDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Spazio non trovato');
  });
});
