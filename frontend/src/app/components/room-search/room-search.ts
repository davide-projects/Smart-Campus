import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, map, of, switchMap, tap } from 'rxjs';
import { RoomFilter, RoomSummary, RoomType, roomTypeLabel } from '../../models/room.model';
import { RoomsService } from '../../services/rooms.service';

interface BuildingOption {
  id: string;
  name: string;
}

export const CAPACITY_DEBOUNCE_MS = 300;

export type ResultsView = 'cards' | 'table';

// La vista scelta resta per tutta la sessione della scheda, anche cambiando pagina.
export const VIEW_STORAGE_KEY = 'smartcampus.roomSearchView';

type SearchResult = { rooms: RoomSummary[] } | { error: true };

@Component({
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSliderModule,
    MatTableModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  selector: 'app-room-search',
  styleUrl: './room-search.scss',
  templateUrl: './room-search.html',
})
export class RoomSearch {
  private readonly roomsService = inject(RoomsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searches = new Subject<RoomFilter>();
  private readonly capacityChanges = new Subject<void>();

  protected readonly typeLabel = roomTypeLabel;
  protected readonly columns: (keyof RoomSummary | 'building' | 'bookable')[] = [
    'name',
    'type',
    'building',
    'capacity',
    'bookable',
  ];

  protected readonly filters = new FormGroup({
    type: new FormControl<RoomType | null>(null),
    minCapacity: new FormControl<number>(0, { nonNullable: true }),
    buildingId: new FormControl<string | null>(null),
  });

  protected readonly rooms = signal<RoomSummary[]>([]);
  protected readonly allRooms = signal<RoomSummary[]>([]);
  protected readonly types = signal<RoomType[]>([]);
  protected readonly buildings = signal<BuildingOption[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);

  // Vista scelta dall'utente (card o tabella). Inizializzata dal sessionStorage.
  protected readonly view = signal<ResultsView>(initialView());

  // Il massimo dello slider corrisponde alla capienza più alta tra tutti gli spazi attivi,
  // così la corsa dello slider è proporzionata al campus reale.
  protected readonly maxCapacity = computed(() =>
    this.allRooms().reduce((max, room) => Math.max(max, room.capacity), 0),
  );

  // toSignal con valore iniziale evita che il testo della capienza compaia solo dopo il primo tocco.
  private readonly minCapacityValue = toSignal(
    this.filters.controls.minCapacity.valueChanges,
    { initialValue: this.filters.controls.minCapacity.value },
  );

  protected readonly capacityText = computed(() => {
    const value = this.minCapacityValue();
    return value > 0 ? `almeno ${value} posti` : 'qualsiasi';
  });

  constructor() {
    this.loadFilterOptions();

    // Lo slider invia la ricerca con un ritardo: trascinandolo non parte una richiesta per ogni scatto.
    this.capacityChanges
      .pipe(debounceTime(CAPACITY_DEBOUNCE_MS), takeUntilDestroyed())
      .subscribe(() => this.triggerSearch());

    this.searches
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadFailed.set(false);
        }),
        // switchMap: se l'utente rilancia la ricerca, la risposta precedente viene ignorata.
        switchMap((filter) =>
          this.roomsService.search(filter).pipe(
            map((rooms): SearchResult => ({ rooms })),
            catchError(() => of<SearchResult>({ error: true })),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if ('rooms' in result) {
          this.rooms.set(result.rooms);
        } else {
          this.rooms.set([]);
          this.loadFailed.set(true);
        }
      });

    this.triggerSearch();
  }

  protected search(): void {
    this.triggerSearch();
  }

  // Notifica la modifica dello slider per applicare il debounce.
  protected capacityChanged(): void {
    this.capacityChanges.next();
  }

  protected capacityLabel(value: number): string {
    return value > 0 ? `${value}` : 'tutti';
  }

  protected resetFilters(): void {
    this.filters.reset({ type: null, minCapacity: 0, buildingId: null });
    // Se il primo caricamento delle opzioni non era riuscito, è l'occasione per riprovare.
    if (this.types().length === 0) {
      this.loadFilterOptions();
    }
    this.triggerSearch();
  }

  protected setView(view: ResultsView): void {
    this.view.set(view);
    try {
      sessionStorage.setItem(VIEW_STORAGE_KEY, view);
    } catch {
      // Navigazione anonima o storage disabilitato: l'impostazione resta valida solo in memoria.
    }
  }

  private triggerSearch(): void {
    const { type, minCapacity, buildingId } = this.filters.getRawValue();
    this.searches.next({
      type,
      minCapacity: minCapacity > 0 ? minCapacity : null,
      buildingId,
    });
  }

  // Tipi, edifici e massimo dello slider si ricavano da tutti gli spazi attivi, così un tipo
  // o un edificio nuovo compare senza modificare il frontend. È una richiesta separata dalla
  // ricerca: premere «Cerca» durante il primo caricamento non la annulla.
  private loadFilterOptions(): void {
    this.roomsService
      .search()
      .pipe(
        catchError(() => of<RoomSummary[]>([])),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((rooms) => {
        this.allRooms.set(rooms);
        this.types.set(typesOf(rooms));
        this.buildings.set(buildingsOf(rooms));
      });
  }
}

function initialView(): ResultsView {
  try {
    const saved = sessionStorage.getItem(VIEW_STORAGE_KEY);
    return saved === 'table' ? 'table' : 'cards';
  } catch {
    return 'cards';
  }
}

function typesOf(rooms: RoomSummary[]): RoomType[] {
  return [...new Set(rooms.map((room) => room.type))].sort((a, b) =>
    roomTypeLabel(a).localeCompare(roomTypeLabel(b), 'it'),
  );
}

function buildingsOf(rooms: RoomSummary[]): BuildingOption[] {
  const byId = new Map(rooms.map((room) => [room.buildingId, room.buildingName]));
  return [...byId]
    .map(([id, name]) => ({ id, name }))
    .sort((a, b) => a.name.localeCompare(b.name, 'it'));
}
