import { Routes } from '@angular/router';
import { authGuard, staffGuard } from './services/auth.guards';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Home',
    loadComponent: () => import('./components/home/home').then((m) => m.Home),
  },
  {
    path: 'login',
    title: 'Accedi',
    loadComponent: () => import('./components/login/login').then((m) => m.Login),
  },
  {
    path: 'rooms',
    title: 'Cerca uno spazio',
    loadComponent: () =>
      import('./components/room-search/room-search').then((m) => m.RoomSearch),
  },
  {
    path: 'rooms/:id',
    title: 'Dettaglio spazio',
    loadComponent: () =>
      import('./components/room-detail/room-detail').then((m) => m.RoomDetail),
  },
  {
    path: 'rooms/:id/book',
    title: 'Prenota spazio',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/room-booking/room-booking').then((m) => m.RoomBooking),
  },
  {
    path: 'my-bookings',
    title: 'Le mie prenotazioni',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/my-bookings/my-bookings').then((m) => m.MyBookings),
  },
  {
    path: 'staff/rooms/new',
    title: 'Nuovo spazio',
    canActivate: [staffGuard],
    loadComponent: () => import('./components/room-form/room-form').then((m) => m.RoomForm),
  },
  {
    path: 'staff/rooms/:id/edit',
    title: 'Modifica spazio',
    canActivate: [staffGuard],
    loadComponent: () => import('./components/room-form/room-form').then((m) => m.RoomForm),
  },
];
