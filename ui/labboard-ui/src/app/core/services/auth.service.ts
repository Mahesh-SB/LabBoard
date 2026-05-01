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
  tokenExpiry: number;
}

export interface ClientAppResponse {
  id: string;
  appName: string;
  appDescription: string;
  clientId: string;
  clientSecret: string;
  grantTypes: string[];
  redirectUris: string[];
  openIdScopes: string[];
  tokenExpiry: number;
  isActive: boolean;
  createdAt: string;
}

export interface ApiPrivilegeEntry {
  targetClientId: string;
  targetAppName: string;
  canRead: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

export interface ApiPrivilegeResponse {
  sourceClientId: string;
  privileges: ApiPrivilegeEntry[];
}

export interface ApiPrivilegeRequest {
  privileges: { targetClientId: string; canRead: boolean; canUpdate: boolean; canDelete: boolean }[];
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

  getAllClientApps(): Observable<ClientAppResponse[]> {
    return this.http.get<ClientAppResponse[]>(this.clientAppsUrl);
  }

  getAppPrivileges(id: string): Observable<ApiPrivilegeResponse> {
    return this.http.get<ApiPrivilegeResponse>(`${this.clientAppsUrl}/${id}/privileges`);
  }

  setAppPrivileges(id: string, request: ApiPrivilegeRequest): Observable<ApiPrivilegeResponse> {
    return this.http.put<ApiPrivilegeResponse>(`${this.clientAppsUrl}/${id}/privileges`, request);
  }
}
