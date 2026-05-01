import { Routes } from '@angular/router';
import { HomeComponent } from './features/home/home.component';
import { UserRegistrationComponent } from './features/auth/user-registration/user-registration.component';
import { ClientRegistrationComponent } from './features/auth/client-registration/client-registration.component';
import { RegisteredAppsComponent } from './features/auth/registered-apps/registered-apps.component';
import { BookTicketComponent } from './features/tickets/book-ticket/book-ticket.component';

export const routes: Routes = [
  { path: '',                component: HomeComponent,               title: 'LabBoard | Home' },
  { path: 'register',        component: UserRegistrationComponent,   title: 'LabBoard | User Registration' },
  { path: 'client-register', component: ClientRegistrationComponent, title: 'LabBoard | Client Registration' },
  { path: 'registered-apps', component: RegisteredAppsComponent,     title: 'LabBoard | Registered Apps' },
  { path: 'tickets/book',    component: BookTicketComponent,          title: 'LabBoard | Book Ticket' },
];
