import { Component } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss'
})
export class NavbarComponent {
  constructor(private router: Router) {}

  get isHome(): boolean {
    return this.router.url === '/';
  }

  signIn(): void {
    window.location.href = `${environment.gatewayBaseUrl}/oauth/start`;
  }

  signOut(): void {
    window.location.href = `${environment.gatewayBaseUrl}/oauth/logout`;
  }
}
