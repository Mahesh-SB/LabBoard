import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BookTicketRequest {
  eventName: string;
  bookedBy:  string;
  seat:      string;
}

export interface BookTicketResponse {
  id:        string;
  eventName: string;
  bookedBy:  string;
  seat:      string;
  bookedAt:  string;
}

@Injectable({ providedIn: 'root' })
export class TicketService {
  private readonly url = `${environment.gatewayBaseUrl}/ticketmaster/api/tickets`;

  constructor(private http: HttpClient) {}

  book(request: BookTicketRequest): Observable<BookTicketResponse> {
    return this.http.post<BookTicketResponse>(this.url, request, {
      withCredentials: true
    });
  }
}
