import { Routes } from '@angular/router';
import { RoomDetail } from './components/shared/room-detail/room-detail';

export const routes: Routes = [
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
    component: RoomDetail,
  },
];
