import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Subject, catchError, map, of, switchMap, tap } from 'rxjs';
import { RoomFilter, RoomSummary, RoomType, roomTypeLabel } from '../../models/room.model';
import { RoomsService } from '../../services/rooms.service';

interface BuildingOption {
  id: string;
  name: string;
}

type SearchResult = { rooms: RoomSummary[] } | { error: true };

@Component({
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  selector: 'app-room-search',
  styleUrl: './room-search.scss',
  templateUrl: './room-search.html',
})
export class RoomSearch {
  private readonly roomsService = inject(RoomsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searches = new Subject<RoomFilter>();

  protected readonly typeLabel = roomTypeLabel;

  protected readonly filters = new FormGroup({
    type: new FormControl<RoomType | null>(null),
    minCapacity: new FormControl<number | null>(null, [
      Validators.min(1),
      Validators.pattern(/^\d+$/),
    ]),
    buildingId: new FormControl<string | null>(null),
  });

  protected readonly rooms = signal<RoomSummary[]>([]);
  protected readonly types = signal<RoomType[]>([]);
  protected readonly buildings = signal<BuildingOption[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);

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

    this.searches.next({});
  }

  protected search(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const { type, minCapacity, buildingId } = this.filters.getRawValue();
    this.searches.next({ type, minCapacity, buildingId });
  }

  protected resetFilters(): void {
    this.filters.reset();
    // Se il primo caricamento dei filtri non era riuscito, è l'occasione per riprovare.
    if (this.types().length === 0) {
      this.loadFilterOptions();
    }
    this.searches.next({});
  }

  // Tipi ed edifici dei filtri si ricavano da tutti gli spazi attivi, così un tipo o un edificio
  // nuovo compare senza modificare il frontend. È una richiesta separata dalla ricerca:
  // premere «Cerca» durante il primo caricamento non la annulla.
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
      });
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
