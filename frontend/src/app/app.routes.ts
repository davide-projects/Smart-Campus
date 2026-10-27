import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'rooms',
    title: 'Cerca uno spazio',
    loadComponent: () =>
      import('./components/room-search/room-search').then((m) => m.RoomSearch),
  },
];
