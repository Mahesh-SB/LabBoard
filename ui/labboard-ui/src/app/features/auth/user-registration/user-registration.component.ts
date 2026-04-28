import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AuthService, UserRegisterRequest } from '../../../core/services/auth.service';

@Component({
  selector: 'app-user-registration',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './user-registration.component.html',
  styleUrl: './user-registration.component.scss'
})
export class UserRegistrationComponent {
  selectedGender = '';
  selectedRole = 'Viewer';
  showPassword = false;
  isLoading = false;
  successMessage = '';
  errorMessage = '';

  genders = ['Male', 'Female', 'Other'];
  roles = ['Admin', 'Editor', 'Viewer'];

  formData = {
    fullName: '',
    email: '',
    phone: '',
    age: null as number | null,
    password: ''
  };

  constructor(private authService: AuthService) {}

  onSubmit(): void {
    this.successMessage = '';
    this.errorMessage = '';

    if (!this.formData.fullName || !this.formData.email || !this.formData.phone ||
        !this.formData.age || !this.selectedGender || !this.formData.password || !this.selectedRole) {
      this.errorMessage = 'Please fill in all required fields.';
      return;
    }

    const request: UserRegisterRequest = {
      fullName: this.formData.fullName,
      email: this.formData.email,
      phone: this.formData.phone,
      age: this.formData.age,
      gender: this.selectedGender,
      password: this.formData.password,
      role: this.selectedRole
    };

    this.isLoading = true;
    this.authService.registerUser(request).subscribe({
      next: () => {
        this.isLoading = false;
        this.successMessage = 'Account created successfully! You can now sign in.';
        this.resetForm();
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 409) {
          this.errorMessage = 'An account with this email already exists.';
        } else if (err.status === 400) {
          this.errorMessage = err.error?.message || 'Invalid registration data. Please check your inputs.';
        } else {
          this.errorMessage = 'Registration failed. Please try again later.';
        }
      }
    });
  }

  private resetForm(): void {
    this.formData = { fullName: '', email: '', phone: '', age: null, password: '' };
    this.selectedGender = '';
    this.selectedRole = 'Viewer';
    this.showPassword = false;
  }
}
