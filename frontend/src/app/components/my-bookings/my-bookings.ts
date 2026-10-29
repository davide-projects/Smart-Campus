import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabGroup, MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { interval } from 'rxjs';
import {
  MyReservationItem,
  MyReservationsResponse,
  isInCheckInWindow,
  membersLabel,
  organizerLabel,
  reservationTimeLabel,
} from '../../models/booking.model';
import { problemOf } from '../../services/api-problem';
import { BookingsService } from '../../services/bookings.service';
import { CancelBookingDialog } from './cancel-booking-dialog/cancel-booking-dialog';

// Voce come la mostra la pagina: organizer e members sono null sulle prenotazioni singole.
interface ReservationView {
  item: MyReservationItem;
  roomName: string;
  buildingName: string;
  time: string;
  organizer: string | null;
  members: string | null;
  // Solo l'organizzatore, prima dell'inizio e senza check-in, vede «Cancella».
  canCancel: boolean;
  cancelLabel: string;
  // Check-in registrato: la voce mostra «Usata» (solo nelle schede «Future» e «Passate»).
  used: boolean;
  usedId: string;
  // «Check-in» compare sulle voci future senza check-in ed è abilitato solo nella finestra.
  showCheckIn: boolean;
  canCheckIn: boolean;
  checkInLabel: string;
  checkInHintId: string;
}

interface ReservationTab {
  label: string;
  items: ReservationView[];
  emptyMessage: string;
  // Solo la scheda «Future» rimanda alla ricerca degli spazi.
  showSearchLink: boolean;
}

const CHECK_IN_HINT = "Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.";
const CHECK_IN_FAILED_MESSAGE = 'Non è stato possibile registrare il check-in. Riprova più tardi.';

// Errori di cui si mostra il messaggio del servizio: non consentito, inesistente, regola violata.
const STATUSES_WITH_DETAIL = [403, 404, 422];

// Ogni quanto si ricalcola la finestra del check-in a pagina aperta.
const CHECK_IN_REFRESH_MS = 30_000;

@Component({
  imports: [MatButtonModule, MatCardModule, MatProgressBarModule, MatTabsModule, RouterLink],
  selector: 'app-my-bookings',
  styleUrl: './my-bookings.scss',
  templateUrl: './my-bookings.html',
})
export class MyBookings {
  private readonly bookingsService = inject(BookingsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly tabGroup = viewChild(MatTabGroup, { read: ElementRef });

  private readonly reservations = signal<MyReservationsResponse | null>(null);
  // Ora del dispositivo all'ultimo caricamento: decide quali voci future si possono cancellare.
  private readonly loadedAt = signal(0);
  // Ora del dispositivo per la finestra del check-in: aggiornata al caricamento e ogni 30 secondi.
  private readonly now = signal(Date.now());
  // Voci di cui è in corso il check-in: ognuna invia una sola richiesta, le altre restano attive.
  protected readonly checkingIn = signal<ReadonlySet<string>>(new Set());
  // Avviso di errore del check-in, per id di voce.
  protected readonly checkInErrors = signal<Record<string, string>>({});
  protected readonly checkInHint = CHECK_IN_HINT;
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  // Ricaricamento fallito dopo una cancellazione: le schede restano, con un avviso sopra.
  protected readonly reloadFailed = signal(false);

  // Le voci restano nell'ordine ricevuto dall'API.
  protected readonly tabs = computed<ReservationTab[]>(() => {
    const data = this.reservations();
    if (!data) {
      return [];
    }
    const loadedAt = this.loadedAt();
    const now = this.now();
    return [
      {
        label: 'Future',
        items: data.upcoming.map((item) =>
          toView(item, {
            canCancel:
              item.isOrganizer && item.checkedInAt === null && Date.parse(item.start) > loadedAt,
            showCheckIn: item.checkedInAt === null,
            canCheckIn: item.checkedInAt === null && isInCheckInWindow(item, now),
            showUsed: true,
          }),
        ),
        emptyMessage: 'Non hai prenotazioni future.',
        showSearchLink: true,
      },
      {
        label: 'Passate',
        items: data.past.map((item) => toView(item, { ...NO_ACTIONS, showUsed: true })),
        emptyMessage: 'Non hai prenotazioni passate.',
        showSearchLink: false,
      },
      {
        label: 'Cancellate',
        items: data.cancelled.map((item) => toView(item, NO_ACTIONS)),
        emptyMessage: 'Non hai prenotazioni cancellate.',
        showSearchLink: false,
      },
    ];
  });

  constructor() {
    this.load();
    // Ricalcolo della finestra senza chiamare il servizio; si ferma quando si lascia la pagina.
    interval(CHECK_IN_REFRESH_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.now.set(Date.now()));
  }

  protected openCancelDialog(item: MyReservationItem): void {
    this.dialog
      .open<CancelBookingDialog, MyReservationItem, boolean>(CancelBookingDialog, { data: item })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((cancelled) => {
        if (cancelled !== true) {
          return;
        }
        this.snackBar.open('Prenotazione cancellata.', undefined, { duration: 5000 });
        // Il pulsante che ha aperto il dialog sparisce con il ricaricamento: il focus va su «Future».
        this.focusUpcomingTab();
        this.load();
      });
  }

  // Invio immediato, senza conferma. Il pulsante fuori finestra o già in invio non manda nulla.
  protected checkIn(view: ReservationView): void {
    const id = view.item.id;
    if (!view.canCheckIn || this.checkingIn().has(id)) {
      return;
    }
    this.setCheckingIn(id, true);
    this.setCheckInError(id, null);

    this.bookingsService
      .checkIn(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.setCheckingIn(id, false);
          this.markCheckedIn(id, response.checkedInAt);
          this.snackBar.open('Check-in registrato.', undefined, { duration: 5000 });
          // Il pulsante che aveva il focus sparisce: il focus va sull'etichetta «Usata».
          afterNextRender(() => this.focusUsedLabel(id), { injector: this.injector });
        },
        error: (error: unknown) => {
          this.setCheckingIn(id, false);
          const problem = problemOf(error);
          // Con 401 l'interceptor ha già chiuso la sessione e portato al login.
          if (problem.status === 401) {
            return;
          }
          this.setCheckInError(
            id,
            STATUSES_WITH_DETAIL.includes(problem.status) && problem.detail
              ? problem.detail
              : CHECK_IN_FAILED_MESSAGE,
          );
        },
      });
  }

  // Il primo caricamento mostra l'indicatore al posto delle schede; il ricaricamento le lascia visibili.
  private load(): void {
    const initial = this.reservations() === null;
    this.reloadFailed.set(false);
    this.bookingsService
      .getMine()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.loadedAt.set(Date.now());
          this.now.set(Date.now());
          // Gli avvisi del check-in si riferiscono all'elenco precedente.
          this.checkInErrors.set({});
          this.reservations.set(data);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          // Con 401 l'interceptor ha già chiuso la sessione e portato al login.
          if (error instanceof HttpErrorResponse && error.status === 401) {
            return;
          }
          if (initial) {
            this.loadFailed.set(true);
          } else {
            this.reloadFailed.set(true);
          }
        },
      });
  }

  // Aggiorna la voce in locale con l'istante della risposta, senza ricaricare l'elenco.
  private markCheckedIn(id: string, checkedInAt: string): void {
    this.reservations.update((data) =>
      data === null
        ? data
        : {
            ...data,
            upcoming: data.upcoming.map((item) =>
              item.id === id ? { ...item, checkedInAt } : item,
            ),
          },
    );
  }

  private setCheckingIn(id: string, pending: boolean): void {
    this.checkingIn.update((ids) => {
      const next = new Set(ids);
      if (pending) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  private setCheckInError(id: string, message: string | null): void {
    this.checkInErrors.update((errors) => {
      const others = { ...errors };
      delete others[id];
      return message === null ? others : { ...others, [id]: message };
    });
  }

  private focusUsedLabel(id: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(`[id="${usedIdOf(id)}"]`)?.focus();
  }

  private focusUpcomingTab(): void {
    const label = this.tabGroup()?.nativeElement.querySelector('[role="tab"]');
    label?.focus();
  }
}

interface ViewActions {
  canCancel: boolean;
  showCheckIn: boolean;
  canCheckIn: boolean;
  // «Usata» compare solo nelle schede «Future» e «Passate».
  showUsed: boolean;
}

const NO_ACTIONS: ViewActions = {
  canCancel: false,
  showCheckIn: false,
  canCheckIn: false,
  showUsed: false,
};

function usedIdOf(id: string): string {
  return `used-${id}`;
}

function toView(item: MyReservationItem, actions: ViewActions): ReservationView {
  const time = reservationTimeLabel(item);
  return {
    item,
    roomName: item.roomName,
    buildingName: item.buildingName ?? '—',
    time,
    organizer: item.isGroup ? organizerLabel(item) : null,
    members: membersLabel(item),
    canCancel: actions.canCancel,
    cancelLabel: `Cancella la prenotazione di ${item.roomName}, ${time}`,
    used: actions.showUsed && item.checkedInAt !== null,
    usedId: usedIdOf(item.id),
    showCheckIn: actions.showCheckIn,
    canCheckIn: actions.canCheckIn,
    checkInLabel: `Check-in per ${item.roomName}, ${time}`,
    checkInHintId: `check-in-hint-${item.id}`,
  };
}
