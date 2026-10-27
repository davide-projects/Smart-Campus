import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { MatSliderHarness } from '@angular/material/slider/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { RoomFilter, RoomSummary } from '../../models/room.model';
import { RoomsService } from '../../services/rooms.service';
import { RoomSearch } from './room-search';

// Dati sintetici di prova.
const ROOMS: RoomSummary[] = [
  {
    id: 'r1', name: 'Aula A1', type: 'Classroom', capacity: 40, floor: 0,
    isBookable: true, buildingId: 'b-a', buildingName: 'Edificio A',
  },
  {
    id: 'r2', name: 'Sala studio B', type: 'StudyRoom', capacity: 20, floor: 1,
    isBookable: false, buildingId: 'b-b', buildingName: 'Edificio B',
  },
];

describe('RoomSearch', () => {
  let fixture: ComponentFixture<RoomSearch>;
  let element: HTMLElement;
  let loader: HarnessLoader;
  let search: ReturnType<typeof vi.fn<(filter?: RoomFilter) => Observable<RoomSummary[]>>>;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [RoomSearch],
      providers: [{ provide: RoomsService, useValue: { search } }],
    }).compileComponents();

    fixture = TestBed.createComponent(RoomSearch);
    element = fixture.nativeElement;
    loader = TestbedHarnessEnvironment.loader(fixture);
    await fixture.whenStable();
  }

  function cardTitles(): string[] {
    return [...element.querySelectorAll('mat-card-title')].map((e) => e.textContent!.trim());
  }

  function select(control: 'type' | 'buildingId'): Promise<MatSelectHarness> {
    return loader.getHarness(MatSelectHarness.with({ selector: `[formControlName="${control}"]` }));
  }

  async function setMinCapacity(value: number): Promise<void> {
    const slider = await loader.getHarness(MatSliderHarness);
    await (await slider.getEndThumb()).setValue(value);
    await fixture.whenStable();
  }

  function capacityText(): string {
    return element.querySelector('.capacity-value')!.textContent!.trim();
  }

  async function optionTexts(control: 'type' | 'buildingId'): Promise<string[]> {
    const harness = await select(control);
    await harness.open();
    const texts = await Promise.all((await harness.getOptions()).map((option) => option.getText()));
    await harness.close();
    return texts;
  }

  async function click(text: string): Promise<void> {
    const buttons = await loader.getAllHarnesses(MatButtonHarness.with({ text }));
    await buttons[0].click();
    await fixture.whenStable();
  }

  beforeEach(() => {
    search = vi.fn(() => of(ROOMS));
  });

  it('shows all rooms with name, type, building and capacity on load', async () => {
    await render();

    expect(search).toHaveBeenCalledWith({});
    expect(cardTitles()).toEqual(['Aula A1', 'Sala studio B']);
    const card = element.querySelector('mat-card')!.textContent!;
    expect(card).toContain('Aula');
    expect(card).toContain('Edificio A');
    expect(card).toContain('40 posti');
  });

  it('marks only the rooms that cannot be booked', async () => {
    await render();

    const cards = [...element.querySelectorAll('mat-card')].map((card) => card.textContent!);
    expect(cards[0]).not.toContain('Non prenotabile');
    expect(cards[1]).toContain('Non prenotabile');
  });

  it('offers the types and buildings of the existing rooms in the filters', async () => {
    await render();

    expect(await optionTexts('type')).toEqual(['Tutti i tipi', 'Aula', 'Sala studio']);
    expect(await optionTexts('buildingId')).toEqual(['Tutti gli edifici', 'Edificio A', 'Edificio B']);
  });

  it('shows the backend value for a new type without a label', async () => {
    search.mockReturnValue(of([{ ...ROOMS[0], id: 'r3', name: 'Aula Magna', type: 'Auditorium' }]));
    await render();

    expect(await optionTexts('type')).toEqual(['Tutti i tipi', 'Auditorium']);
    expect(element.querySelector('mat-card-subtitle')!.textContent!.trim()).toBe('Auditorium');
  });

  it('searches as soon as a filter changes, combining all of them', async () => {
    await render();
    search.mockReturnValue(of([ROOMS[1]]));

    await (await select('type')).clickOptions({ text: 'Sala studio' });
    expect(search).toHaveBeenLastCalledWith({ type: 'StudyRoom', minCapacity: null, buildingId: null });

    await setMinCapacity(15);
    expect(search).toHaveBeenLastCalledWith({ type: 'StudyRoom', minCapacity: 15, buildingId: null });

    await (await select('buildingId')).clickOptions({ text: 'Edificio B' });
    expect(search).toHaveBeenLastCalledWith({ type: 'StudyRoom', minCapacity: 15, buildingId: 'b-b' });
    expect(cardTitles()).toEqual(['Sala studio B']);
  });

  it('repeats the search with the current filters when Cerca is pressed', async () => {
    await render();
    await setMinCapacity(15);
    search.mockClear();

    await click('Cerca');

    expect(search).toHaveBeenCalledExactlyOnceWith({ type: null, minCapacity: 15, buildingId: null });
  });

  it('keeps the filter options when Cerca is pressed during the first load', async () => {
    // Prima chiamata: valori dei filtri; seconda: ricerca iniziale, ancora senza risposta.
    const firstSearch = new Subject<RoomSummary[]>();
    search.mockReturnValueOnce(of(ROOMS)).mockReturnValueOnce(firstSearch);
    await render();

    await (await select('type')).clickOptions({ text: 'Aula' });
    await click('Cerca');

    expect(firstSearch.observed).toBe(false);
    expect(cardTitles()).toEqual(['Aula A1', 'Sala studio B']);
    expect(await optionTexts('buildingId')).toEqual(['Tutti gli edifici', 'Edificio A', 'Edificio B']);
  });

  it('limits the capacity slider from 0 to the largest room', async () => {
    await render();

    const thumb = await (await loader.getHarness(MatSliderHarness)).getEndThumb();
    expect(await thumb.getMinValue()).toBe(0);
    expect(await thumb.getMaxValue()).toBe(40);
    expect(await thumb.getValue()).toBe(0);
    expect(capacityText()).toBe('Qualsiasi');
  });

  it('shows the chosen capacity and sends no minimum when it goes back to 0', async () => {
    await render();

    await setMinCapacity(25);
    expect(capacityText()).toBe('Almeno 25 posti');

    await setMinCapacity(0);
    expect(capacityText()).toBe('Qualsiasi');
    expect(search).toHaveBeenLastCalledWith({ type: null, minCapacity: null, buildingId: null });
  });

  it('puts the slider back to 0 when the filters are reset', async () => {
    await render();
    await setMinCapacity(25);

    await click('Azzera filtri');

    expect(await (await (await loader.getHarness(MatSliderHarness)).getEndThumb()).getValue()).toBe(0);
    expect(capacityText()).toBe('Qualsiasi');
    expect(search).toHaveBeenLastCalledWith({});
  });

  it('shows the message on zero results and lets the user reset the filters', async () => {
    search.mockReturnValue(of([]));
    await render();

    expect(element.textContent).toContain('Nessuno spazio corrisponde ai filtri selezionati.');
    expect(element.querySelector('mat-card')).toBeNull();

    search.mockReturnValue(of(ROOMS));
    await click('Azzera filtri');

    expect(search).toHaveBeenLastCalledWith({});
    expect(cardTitles()).toEqual(['Aula A1', 'Sala studio B']);
  });

  it('shows an error message when the service does not respond', async () => {
    search.mockReturnValue(throwError(() => new Error('rete')));
    await render();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(
      'Non è stato possibile caricare gli spazi.',
    );
  });
});
