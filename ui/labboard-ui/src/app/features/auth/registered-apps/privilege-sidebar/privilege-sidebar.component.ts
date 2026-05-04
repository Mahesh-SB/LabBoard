import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ClientAppResponse } from '../../../../core/services/auth.service';

interface PrivilegeRow {
  app: ClientAppResponse;
  canRead: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

@Component({
  selector: 'app-privilege-sidebar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './privilege-sidebar.component.html',
  styleUrl: './privilege-sidebar.component.scss'
})
export class PrivilegeSidebarComponent implements OnChanges {
  @Input() selectedApp: ClientAppResponse | null = null;
  @Input() allApps: ClientAppResponse[] = [];
  @Output() closed = new EventEmitter<void>();
  @Output() saved  = new EventEmitter<void>();

  isEditMode     = false;
  searchQuery    = '';
  rows: PrivilegeRow[] = [];
  isLoading      = false;
  isSaving       = false;
  errorMessage   = '';
  successMessage = '';

  constructor(private authService: AuthService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedApp'] && this.selectedApp) {
      this.isEditMode     = false;
      this.searchQuery    = '';
      this.errorMessage   = '';
      this.successMessage = '';
      this.loadPrivileges();
    }
  }

  enableEdit(): void {
    this.isEditMode    = true;
    this.successMessage = '';
    this.errorMessage  = '';
  }

  cancelEdit(): void {
    this.isEditMode = false;
    this.loadPrivileges();
  }

  get otherApps(): ClientAppResponse[] {
    const q = this.searchQuery.toLowerCase();
    return this.allApps.filter(a =>
      a.id !== this.selectedApp?.id &&
      (a.appName.toLowerCase().includes(q) || a.clientId.toLowerCase().includes(q))
    );
  }

  get grantedRows(): PrivilegeRow[] {
    return this.rows.filter(r => r.canRead || r.canUpdate || r.canDelete);
  }

  isSelected(app: ClientAppResponse): boolean {
    return this.rows.some(r => r.app.clientId === app.clientId);
  }

  getRow(app: ClientAppResponse): PrivilegeRow | undefined {
    return this.rows.find(r => r.app.clientId === app.clientId);
  }

  toggleApp(app: ClientAppResponse): void {
    const idx = this.rows.findIndex(r => r.app.clientId === app.clientId);
    if (idx >= 0) this.rows.splice(idx, 1);
    else          this.rows.push({ app, canRead: false, canUpdate: false, canDelete: false });
  }

  save(): void {
    if (!this.selectedApp) return;
    this.isSaving      = true;
    this.errorMessage  = '';
    this.successMessage = '';

    this.authService.setAppPrivileges(this.selectedApp.id, {
      privileges: this.rows.map(r => ({
        targetClientId: r.app.clientId,
        canRead:   r.canRead,
        canUpdate: r.canUpdate,
        canDelete: r.canDelete
      }))
    }).subscribe({
      next: () => {
        this.isSaving       = false;
        this.isEditMode     = false;
        this.successMessage = 'Privileges saved successfully.';
        this.loadPrivileges();
      },
      error: () => {
        this.isSaving     = false;
        this.errorMessage = 'Failed to save privileges.';
      }
    });
  }

  close(): void { this.closed.emit(); }

  private loadPrivileges(): void {
    if (!this.selectedApp) return;
    this.isLoading = true;
    this.rows      = [];

    this.authService.getAppPrivileges(this.selectedApp.id).subscribe({
      next: (res) => {
        this.isLoading = false;
        this.rows = res.privileges
          .map(p => {
            const app = this.allApps.find(a => a.clientId === p.targetClientId);
            return app ? { app, canRead: p.canRead, canUpdate: p.canUpdate, canDelete: p.canDelete } : null;
          })
          .filter((r): r is PrivilegeRow => r !== null);
      },
      error: () => { this.isLoading = false; }
    });
  }
}
