import { Component, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, FormControl, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AuthService, ClientAppRequest, ClientAppResponse } from '../../../core/services/auth.service';
import { FieldErrorPipe } from '../../../shared/pipes/field-error.pipe';

// ── Field-level validators ────────────────────────────────────────────────────

function atLeastOneValidator(control: AbstractControl): ValidationErrors | null {
  const val = control.value as string[];
  return val?.length > 0 ? null : { atLeastOne: true };
}

function urlValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value?.trim()) return null;
  try { new URL(control.value); return null; }
  catch { return { invalidUrl: true }; }
}

// ── Cross-field (form-level) validators ───────────────────────────────────────

/**
 * refresh_token requires authorization_code — refresh tokens are only issued
 * during the authorization code flow.
 */
function refreshTokenRequiresAuthCode(form: AbstractControl): ValidationErrors | null {
  const gt = form.get('grantTypes')?.value as string[];
  if (gt.includes('refresh_token') && !gt.includes('authorization_code')) {
    return { refreshTokenNeedsAuthCode: true };
  }
  return null;
}

/**
 * OfflineAccess scope requires both authorization_code and refresh_token —
 * it's meaningless in any other flow.
 */
function offlineAccessRequiresAuthCodeAndRefreshToken(form: AbstractControl): ValidationErrors | null {
  const gt = form.get('grantTypes')?.value as string[];
  const oi = form.get('additionalOpenIdScopes')?.value as string[];
  if (oi.includes('OfflineAccess') && (!gt.includes('authorization_code') || !gt.includes('refresh_token'))) {
    return { offlineAccessNeedsAuthCodeAndRefreshToken: true };
  }
  return null;
}

/**
 * authorization_code + refresh_token requires OfflineAccess scope —
 * without it the server won't issue a refresh token even if the grant type is present.
 */
function authCodeAndRefreshTokenRequiresOfflineAccess(form: AbstractControl): ValidationErrors | null {
  const gt = form.get('grantTypes')?.value as string[];
  const oi = form.get('additionalOpenIdScopes')?.value as string[];
  if (gt.includes('authorization_code') && gt.includes('refresh_token') && !oi.includes('OfflineAccess')) {
    return { needsOfflineAccessScope: true };
  }
  return null;
}

// ── Component ─────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-client-registration',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule, FieldErrorPipe],
  templateUrl: './client-registration.component.html',
  styleUrl: './client-registration.component.scss'
})
export class ClientRegistrationComponent implements OnInit {
  form!: FormGroup;
  isLoading = false;
  errorMessage = '';
  registeredApp: ClientAppResponse | null = null;
  copied: 'clientId' | 'clientSecret' | null = null;

  readonly grantTypeOptions   = ['client_credentials', 'authorization_code', 'refresh_token'];
  readonly apiScopeOptions    = ['Admin', 'Add', 'Update', 'Delete'];
  readonly openIdScopeOptions = ['Phone', 'Address', 'OfflineAccess'];

  constructor(private fb: FormBuilder, private authService: AuthService) {}

  ngOnInit(): void {
    this.form = this.fb.group(
      {
        appName:                ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
        appDescription:         ['', [Validators.maxLength(500)]],
        tokenExpiry:            [3600, [Validators.required, Validators.min(1), Validators.max(86400)]],
        grantTypes:             [[] as string[], [atLeastOneValidator]],
        apiScopes:              [[] as string[], [atLeastOneValidator]],
        additionalOpenIdScopes: [[] as string[]],
        redirectUris:           this.fb.array([this.createUriControl()])
      },
      {
        validators: [
          refreshTokenRequiresAuthCode,
          offlineAccessRequiresAuthCodeAndRefreshToken,
          authCodeAndRefreshTokenRequiresOfflineAccess
        ]
      }
    );
  }

  // ── Getters ───────────────────────────────────────────────────────────────

  get redirectUrisArray(): FormArray { return this.form.get('redirectUris') as FormArray; }

  ctrl(name: string): FormControl { return this.form.get(name) as FormControl; }

  /** True only after the user has interacted with grant types or scopes */
  private get crossFieldTouched(): boolean {
    return this.ctrl('grantTypes').touched || this.ctrl('additionalOpenIdScopes').touched;
  }

  get showRefreshTokenNeedsAuthCode(): boolean {
    return this.crossFieldTouched && !!this.form.errors?.['refreshTokenNeedsAuthCode'];
  }

  get showOfflineAccessNeedsAuthCodeAndRefreshToken(): boolean {
    return this.crossFieldTouched && !!this.form.errors?.['offlineAccessNeedsAuthCodeAndRefreshToken'];
  }

  get showNeedsOfflineAccessScope(): boolean {
    return this.crossFieldTouched && !!this.form.errors?.['needsOfflineAccessScope'];
  }

  // ── Chip toggle ───────────────────────────────────────────────────────────

  toggleChip(controlName: 'grantTypes' | 'apiScopes' | 'additionalOpenIdScopes', value: string): void {
    if (this.isLoading) return;
    const control = this.ctrl(controlName);
    const current = control.value as string[];
    const updated = current.includes(value)
      ? current.filter(v => v !== value)
      : [...current, value];
    control.setValue(updated);
    control.markAsTouched();
  }

  isChipSelected(controlName: string, value: string): boolean {
    return (this.ctrl(controlName).value as string[]).includes(value);
  }

  // ── Redirect URIs ─────────────────────────────────────────────────────────

  private createUriControl(): FormControl {
    return this.fb.control('', urlValidator);
  }

  addRedirectUri(): void { this.redirectUrisArray.push(this.createUriControl()); }

  removeRedirectUri(index: number): void {
    if (this.redirectUrisArray.length > 1) this.redirectUrisArray.removeAt(index);
  }

  uriControl(i: number): FormControl { return this.redirectUrisArray.at(i) as FormControl; }

  // ── Clipboard ─────────────────────────────────────────────────────────────

  copy(value: string, key: 'clientId' | 'clientSecret'): void {
    navigator.clipboard.writeText(value).then(() => {
      this.copied = key;
      setTimeout(() => this.copied = null, 2000);
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.errorMessage = '';
    this.registeredApp = null;

    const v = this.form.value;
    const request: ClientAppRequest = {
      appName:                v.appName.trim(),
      appDescription:         v.appDescription.trim(),
      grantTypes:             v.grantTypes,
      redirectUris:           (v.redirectUris as string[]).filter((u: string) => u.trim()),
      additionalOpenIdScopes: v.additionalOpenIdScopes,
      apiScopes:              v.apiScopes,
      tokenExpiry:            v.tokenExpiry
    };

    this.isLoading = true;
    this.authService.registerClientApp(request).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.registeredApp = response;
        this.form.reset({ tokenExpiry: 3600, grantTypes: [], apiScopes: [], additionalOpenIdScopes: [] });
        this.redirectUrisArray.clear();
        this.redirectUrisArray.push(this.createUriControl());
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 409)      this.errorMessage = 'An application with this name already exists.';
        else if (err.status === 400) this.errorMessage = err.error?.message || 'Invalid data. Please check your inputs.';
        else                         this.errorMessage = 'Registration failed. Please try again later.';
      }
    });
  }
}
