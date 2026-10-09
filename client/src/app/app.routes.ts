import { Routes } from '@angular/router';
import { authGuard } from './auth';
import { Login } from './login';
import { TicketDetail } from './ticket-detail';
import { Tickets } from './tickets';

export const routes: Routes = [
  { path: 'login', component: Login, title: 'Sign in' },
  { path: 'tickets', component: Tickets, canActivate: [authGuard], title: 'Tickets' },
  { path: 'tickets/:id', component: TicketDetail, canActivate: [authGuard], title: 'Ticket' },
  { path: '', pathMatch: 'full', redirectTo: 'tickets' },
  { path: '**', redirectTo: 'tickets' },
];
