import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { UserProfile, roleLabel } from '../../../models/user.model';
import { AuthService } from '../../../services/auth.service';

// access: "user" se la pagina richiede il login, "staff" se è riservata al personale.
export interface NavLink {
  path: string;
  label: string;
  access?: 'user' | 'staff';
}

// Voci di navigazione: per una nuova pagina basta aggiungerla qui.
const LINKS: NavLink[] = [
  { path: '/rooms', label: 'Cerca spazi' },
  { path: '/available-rooms', label: 'Spazi liberi', access: 'user' },
  { path: '/my-bookings', label: 'Le mie prenotazioni', access: 'user' },
  { path: '/staff/reservations', label: 'Prenotazioni', access: 'staff' },
  { path: '/staff/rooms/new', label: 'Nuovo spazio', access: 'staff' },
];

// Ognuno vede solo le voci delle pagine che può aprire.
export function visibleLinks(links: NavLink[], user: UserProfile | null): NavLink[] {
  return links.filter(
    (link) =>
      !link.access || (user !== null && (link.access === 'user' || user.role === 'Staff')),
  );
}

@Component({
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatToolbarModule, RouterLink, RouterLinkActive],
  selector: 'app-navbar',
  styleUrl: './navbar.scss',
  templateUrl: './navbar.html',
})
export class Navbar {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly user = this.auth.user;
  // Sulla pagina di login il pulsante «Accedi» non serve.
  protected readonly onLoginPage = computed(() => this.url().startsWith('/login'));
  protected readonly links = computed(() => visibleLinks(LINKS, this.user()));
  protected readonly roleLabel = roleLabel;

  protected logout(): void {
    this.auth.logout();
  }
}
