import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UserRegisterRequest {
  fullName: string;
  email: string;
  phone: string;
  age: number;
  gender: string;
  password: string;
  role: string;
}

export interface ClientAppRequest {
  appName: string;
  appDescription: string;
  grantTypes: string[];
  redirectUris: string[];
  additionalOpenIdScopes: string[];
  apiScopes: string[];
  tokenExpiry: number;
}

export interface ClientAppResponse {
  id: string;
  appName: string;
  clientId: string;
  clientSecret: string;
  grantTypes: string[];
  redirectUris: string[];
  openIdScopes: string[];
  apiScopes: string[];
  tokenExpiry: number;
  isActive: boolean;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly usersUrl = `${environment.apiBaseUrl}/api/users`;
  private readonly clientAppsUrl = `${environment.apiBaseUrl}/api/clientapps`;

  constructor(private http: HttpClient) {}

  registerUser(request: UserRegisterRequest): Observable<unknown> {
    return this.http.post(`${this.usersUrl}/register`, request);
  }

  registerClientApp(request: ClientAppRequest): Observable<ClientAppResponse> {
    return this.http.post<ClientAppResponse>(`${this.clientAppsUrl}/register`, request);
  }
}
