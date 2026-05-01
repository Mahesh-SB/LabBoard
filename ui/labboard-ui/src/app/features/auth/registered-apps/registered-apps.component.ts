import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService, ClientAppResponse } from '../../../core/services/auth.service';
import { PrivilegeSidebarComponent } from './privilege-sidebar/privilege-sidebar.component';

@Component({
  selector: 'app-registered-apps',
  standalone: true,
  imports: [CommonModule, PrivilegeSidebarComponent],
  templateUrl: './registered-apps.component.html',
  styleUrl: './registered-apps.component.scss'
})
export class RegisteredAppsComponent implements OnInit {
  apps: ClientAppResponse[] = [];
  isLoading    = true;
  errorMessage = '';
  selectedApp: ClientAppResponse | null = null;
  copiedKey: string | null = null;

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.loadApps();
  }

  loadApps(): void {
    this.isLoading = true;
    this.authService.getAllClientApps().subscribe({
      next: (apps) => { this.apps = apps; this.isLoading = false; },
      error: () => { this.errorMessage = 'Failed to load registered applications.'; this.isLoading = false; }
    });
  }

  openPrivileges(app: ClientAppResponse): void {
    this.selectedApp = app;
  }

  closeSidebar(): void {
    this.selectedApp = null;
  }

  copy(value: string, key: string): void {
    navigator.clipboard.writeText(value).then(() => {
      this.copiedKey = key;
      setTimeout(() => this.copiedKey = null, 2000);
    });
  }

  isCopied(key: string): boolean {
    return this.copiedKey === key;
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
