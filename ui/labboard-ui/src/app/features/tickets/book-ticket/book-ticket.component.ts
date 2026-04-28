import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService } from '../../../core/services/ticket.service';

const PENDING_BOOKING_KEY = 'pending_booking';

@Component({
  selector: 'app-book-ticket',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './book-ticket.component.html',
  styleUrl: './book-ticket.component.scss'
})
export class BookTicketComponent implements OnInit, OnDestroy {
  readonly eventName    = 'LabBoard Music Festival 2026';
  readonly eventDate    = 'April 30, 2026';
  readonly eventVenue   = 'Mumbai Arena, Maharashtra';
  readonly pricePerSeat = '₹2,499';

  form = { personName: '', age: null as number | null, seats: '' };

  seatOptions = [1, 2, 3, 4, 5, 6, 7, 8];
  isLoading   = false;

  toast: { message: string; type: 'success' | 'error' } | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private ticketService: TicketService) {}

  ngOnInit(): void {
    const pending = sessionStorage.getItem(PENDING_BOOKING_KEY);
    if (pending) {
      this.form = JSON.parse(pending);
      this.submitBooking();
    }
  }

  onBook(): void {
    if (!this.form.personName || !this.form.age || !this.form.seats) {
      this.showToast('Please fill in all fields before confirming.', 'error');
      return;
    }
    this.submitBooking();
  }

  private submitBooking(): void {
    this.isLoading = true;
    sessionStorage.setItem(PENDING_BOOKING_KEY, JSON.stringify(this.form));

    this.ticketService.book({
      eventName: this.eventName,
      bookedBy:  this.form.personName,
      seat:      `${this.form.seats} Seat(s)`
    }).subscribe({
      next: (res) => {
        this.isLoading = false;
        sessionStorage.removeItem(PENDING_BOOKING_KEY);
        this.showToast(`Booking confirmed! ${res.seat} reserved for ${res.bookedBy}.`, 'success');
        this.form = { personName: '', age: null, seats: '' };
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status !== 401) {
          sessionStorage.removeItem(PENDING_BOOKING_KEY);
          this.showToast('Booking failed. Please try again.', 'error');
        }
        // On 401: keep sessionStorage intact — interceptor redirects to OAuth,
        // and ngOnInit will auto-retry the booking after returning with a valid session.
      }
    });
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toast = { message, type };
    this.toastTimer = setTimeout(() => (this.toast = null), 8000);
  }

  ngOnDestroy(): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
  }
}
