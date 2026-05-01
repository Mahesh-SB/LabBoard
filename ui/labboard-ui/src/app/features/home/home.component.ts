import { Component, ElementRef, HostListener } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';

interface SearchItem {
  icon: string;
  title: string;
  desc: string;
  route: string;
  group: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  searchQuery = '';
  searchOpen  = false;

  private readonly searchItems: SearchItem[] = [
    { icon: '🏠', title: 'Home',              desc: 'Dashboard & overview',      route: '/',                 group: 'General' },
    { icon: '🖥️', title: 'Client App',        desc: 'Register OAuth clients',    route: '/client-register',  group: 'Auth'    },
    { icon: '📋', title: 'Registered Apps',   desc: 'View apps & set privileges', route: '/registered-apps', group: 'Auth'    },
    { icon: '👤', title: 'User Registration', desc: 'Create & manage users',     route: '/register',         group: 'Auth'    },
    { icon: '🎫', title: 'Ticket Master',     desc: 'Book & manage tickets',     route: '/tickets/book',     group: 'Apps'    },
  ];

  get results(): SearchItem[] {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return [];
    return this.searchItems.filter(i =>
      i.title.toLowerCase().includes(q) ||
      i.desc.toLowerCase().includes(q)  ||
      i.group.toLowerCase().includes(q)
    );
  }

  constructor(private router: Router, private elRef: ElementRef) {}

  onSearchInput(): void {
    this.searchOpen = this.searchQuery.trim().length > 0;
  }

  navigate(route: string): void {
    this.router.navigateByUrl(route);
    this.searchQuery = '';
    this.searchOpen  = false;
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.searchOpen  = false;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(e: Event): void {
    if (!this.elRef.nativeElement.contains(e.target)) {
      this.searchOpen = false;
    }
  }

  signIn(): void {
    window.location.href = `${environment.gatewayBaseUrl}/oauth/start`;
  }

  services = [
    {
      icon: '🔐',
      name: 'Auth API',
      desc: 'OAuth 2.0 & JWT token issuer',
      port: '5100',
      color: '#818cf8',
    },
    {
      icon: '🌐',
      name: 'Gateway',
      desc: 'Ocelot reverse proxy / BFF',
      port: '5200',
      color: '#34d399',
    },
    {
      icon: '💾',
      name: 'Redis API',
      desc: 'High-speed caching layer',
      port: '5000',
      color: '#f87171',
    },
    {
      icon: '📊',
      name: 'Observability',
      desc: 'Prometheus metrics scraper',
      port: '5001',
      color: '#fbbf24',
    },
  ];

  authLinks = [
    {
      icon: '🖥️',
      title: 'Client App',
      desc: 'Register & manage OAuth client applications',
      route: '/client-register',
      gradient: 'linear-gradient(135deg, #818cf8, #6c63ff)',
    },
    {
      icon: '📋',
      title: 'Registered Apps',
      desc: 'View registered apps & set API privileges',
      route: '/registered-apps',
      gradient: 'linear-gradient(135deg, #f59e0b, #d97706)',
    },
    {
      icon: '👤',
      title: 'User Registration',
      desc: 'Create accounts and assign roles',
      route: '/register',
      gradient: 'linear-gradient(135deg, #34d399, #059669)',
    },
  ];

  appLinks = [
    {
      icon: '🎫',
      title: 'Ticket Master',
      desc: 'Book and manage event tickets',
      route: '/tickets/book',
      gradient: 'linear-gradient(135deg, #f87171, #dc2626)',
    },
  ];

  stack = [
    { label: '.NET 9',     gradient: 'linear-gradient(135deg,#6366f1,#4f46e5)', glow: 'rgba(99,102,241,0.5)'  },
    { label: 'Redis',      gradient: 'linear-gradient(135deg,#ef4444,#b91c1c)', glow: 'rgba(239,68,68,0.5)'   },
    { label: 'Ocelot',     gradient: 'linear-gradient(135deg,#8b5cf6,#6d28d9)', glow: 'rgba(139,92,246,0.5)'  },
    { label: 'Prometheus', gradient: 'linear-gradient(135deg,#f97316,#c2410c)', glow: 'rgba(249,115,22,0.5)'  },
    { label: 'Grafana',    gradient: 'linear-gradient(135deg,#22c55e,#15803d)', glow: 'rgba(34,197,94,0.5)'   },
    { label: 'k6',         gradient: 'linear-gradient(135deg,#eab308,#a16207)', glow: 'rgba(234,179,8,0.5)'   },
    { label: 'Angular',    gradient: 'linear-gradient(135deg,#f43f5e,#be123c)', glow: 'rgba(244,63,94,0.5)'   },
  ];
}
