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
import {
  BuildingOption,
  RoomFilter,
  RoomSummary,
  RoomType,
  buildingsOf,
  roomTypeLabel,
} from '../../models/room.model';
import { RoomsService } from '../../services/rooms.service';

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

  protected readonly filters = new FormGroup({
    type: new FormControl<RoomType | null>(null),
    // 0 vuol dire nessuna capienza minima/massima.
    minCapacity: new FormControl(0, { nonNullable: true }),
    maxCapacity: new FormControl(0, { nonNullable: true }),
    buildingId: new FormControl<string | null>(null),
  });

  protected readonly rooms = signal<RoomSummary[]>([]);
  protected readonly types = signal<RoomType[]>([]);
  protected readonly buildings = signal<BuildingOption[]>([]);
  protected readonly maxCapacity = signal(0);
  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly view = signal<ResultsView>(savedView());
  protected readonly columns = ['name', 'type', 'building', 'capacity', 'bookable'];

  private readonly minCapacity = toSignal(this.filters.controls.minCapacity.valueChanges, {
    initialValue: 0,
  });
  private readonly maxCapacityFilter = toSignal(this.filters.controls.maxCapacity.valueChanges, {
    initialValue: 0,
  });
  protected readonly capacityText = computed(() => {
    const min = this.minCapacity();
    const max = this.maxCapacityFilter();
    if (min <= 0 && (max <= 0 || max >= this.maxCapacity())) {
      return 'Qualsiasi';
    }
    if (min > 0 && max > 0 && max < this.maxCapacity()) {
      return `Da ${min} a ${max} posti`;
    }
    if (min > 0) {
      return min === 1 ? 'Almeno 1 posto' : `Almeno ${min} posti`;
    }
    return `Fino a ${max} posti`;
  });
  // Lo slider usa lo stesso testo anche per i lettori di schermo (aria-valuetext).
  protected readonly capacityLabel = capacityLabel;

  constructor() {
    this.loadFilterOptions();

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

    // Da tastiera ogni freccia cambia il valore: si cerca solo quando lo slider
    // resta fermo per 300 ms.
    this.capacityChanges
      .pipe(debounceTime(CAPACITY_DEBOUNCE_MS), takeUntilDestroyed())
      .subscribe(() => this.search());

    this.searches.next({});
  }

  protected setView(view: ResultsView): void {
    this.view.set(view);
    try {
      sessionStorage.setItem(VIEW_STORAGE_KEY, view);
    } catch {
      // Senza sessionStorage la vista vale solo finché si resta sulla pagina.
    }
  }

  protected capacityChanged(): void {
    this.capacityChanges.next();
  }

  // Parte quando cambia un menu, quando si ferma lo slider e con «Cerca».
  protected search(): void {
    const { type, minCapacity, maxCapacity, buildingId } = this.filters.getRawValue();
    const max = this.maxCapacity();
    this.searches.next({
      type,
      minCapacity: minCapacity > 0 ? minCapacity : null,
      maxCapacity: maxCapacity > 0 && maxCapacity < max ? maxCapacity : null,
      buildingId,
    });
  }

  protected resetFilters(): void {
    this.filters.reset();
    // Se il primo caricamento dei filtri non era riuscito, è l'occasione per riprovare.
    if (this.types().length === 0) {
      this.loadFilterOptions();
    }
    this.searches.next({});
  }

  // Tipi, edifici e capienza massima dei filtri si ricavano da tutti gli spazi attivi,
  // così un tipo o un edificio nuovo compare senza modificare il frontend.
  // È una richiesta separata dalla ricerca: premere «Cerca» durante il primo caricamento
  // non la annulla.
  private loadFilterOptions(): void {
    this.roomsService
      .search()
      .pipe(
        catchError(() => of<RoomSummary[]>([])),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((rooms) => {
        this.types.set(typesOf(rooms));
        this.buildings.set(buildingsOf(rooms));
        this.maxCapacity.set(Math.max(0, ...rooms.map((room) => room.capacity)));
      });
  }
}

function savedView(): ResultsView {
  try {
    return sessionStorage.getItem(VIEW_STORAGE_KEY) === 'table' ? 'table' : 'cards';
  } catch {
    return 'cards';
  }
}

function capacityLabel(minCapacity: number): string {
  if (minCapacity <= 0) {
    return 'Qualsiasi';
  }
  return minCapacity === 1 ? 'Almeno 1 posto' : `Almeno ${minCapacity} posti`;
}

function typesOf(rooms: RoomSummary[]): RoomType[] {
  return [...new Set(rooms.map((room) => room.type))].sort((a, b) =>
    roomTypeLabel(a).localeCompare(roomTypeLabel(b), 'it'),
  );
}
