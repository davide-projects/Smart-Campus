import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { RoomFilter, RoomSummary } from '../shared/room.model';
import { RoomsService } from '../shared/rooms.service';
import { RoomSearch } from './room-search';

// Dati sintetici di prova.
const ROOMS: RoomSummary[] = [
  {
    id: 'r1', name: 'Aula A1', type: 'Classroom', capacity: 40, floor: 0,
    isBookable: true, buildingId: 'b-a', buildingName: 'Edificio A',
  },
  {
    id: 'r2', name: 'Sala studio B', type: 'StudyRoom', capacity: 20, floor: 1,
    isBookable: true, buildingId: 'b-b', buildingName: 'Edificio B',
  },
];

describe('RoomSearch', () => {
  let fixture: ComponentFixture<RoomSearch>;
  let element: HTMLElement;
  let search: ReturnType<typeof vi.fn<(filter?: RoomFilter) => Observable<RoomSummary[]>>>;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [RoomSearch],
      providers: [{ provide: RoomsService, useValue: { search } }],
    }).compileComponents();

    fixture = TestBed.createComponent(RoomSearch);
    element = fixture.nativeElement;
    await fixture.whenStable();
  }

  function cardTitles(): string[] {
    return [...element.querySelectorAll('mat-card-title')].map((e) => e.textContent!.trim());
  }

  function buttonWithText(text: string): HTMLButtonElement {
    return [...element.querySelectorAll('button')].find((b) => b.textContent!.trim() === text)!;
  }

  beforeEach(() => {
    search = vi.fn(() => of(ROOMS));
  });

  it('all\'apertura mostra tutti gli spazi con nome, tipo, edificio e capienza', async () => {
    await render();

    expect(search).toHaveBeenCalledWith({});
    expect(cardTitles()).toEqual(['Aula A1', 'Sala studio B']);
    const card = element.querySelector('mat-card')!.textContent!;
    expect(card).toContain('Aula');
    expect(card).toContain('Edificio A');
    expect(card).toContain('40 posti');
  });

  it('ricava gli edifici del filtro dalla ricerca senza filtri', async () => {
    await render();

    const component = fixture.componentInstance as unknown as { buildings: () => unknown };
    expect(component.buildings()).toEqual([
      { id: 'b-a', name: 'Edificio A' },
      { id: 'b-b', name: 'Edificio B' },
    ]);
  });

  it('premendo Cerca invia tutti i filtri impostati', async () => {
    await render();
    const component = fixture.componentInstance as unknown as RoomSearchInternals;
    search.mockReturnValue(of([ROOMS[1]]));

    component.filters.setValue({ type: 'StudyRoom', minCapacity: 30, buildingId: 'b-b' });
    buttonWithText('Cerca').click();
    await fixture.whenStable();

    expect(search).toHaveBeenLastCalledWith({ type: 'StudyRoom', minCapacity: 30, buildingId: 'b-b' });
    expect(cardTitles()).toEqual(['Sala studio B']);
  });

  it('non cerca se la capienza minima non è valida', async () => {
    await render();
    const component = fixture.componentInstance as unknown as RoomSearchInternals;

    component.filters.controls.minCapacity.setValue(0);
    buttonWithText('Cerca').click();
    await fixture.whenStable();

    expect(search).toHaveBeenCalledTimes(1);
    expect(element.textContent).toContain('Inserisci un numero intero maggiore di zero.');
  });

  it('con zero risultati mostra il messaggio e permette di azzerare i filtri', async () => {
    search.mockReturnValue(of([]));
    await render();

    expect(element.textContent).toContain('Nessuno spazio corrisponde ai filtri selezionati.');
    expect(element.querySelector('mat-card')).toBeNull();

    search.mockReturnValue(of(ROOMS));
    buttonWithText('Azzera filtri').click();
    await fixture.whenStable();

    expect(search).toHaveBeenLastCalledWith({});
    expect(cardTitles()).toEqual(['Aula A1', 'Sala studio B']);
  });

  it('se il servizio non risponde mostra un messaggio di errore', async () => {
    search.mockReturnValue(throwError(() => new Error('rete')));
    await render();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(
      'Non è stato possibile caricare gli spazi.',
    );
  });
});

interface RoomSearchInternals {
  filters: RoomSearch['filters'];
}
