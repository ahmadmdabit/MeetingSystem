import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { AuthResponse, Login, RegisterUser } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly baseUrl = `${this.config.baseUrl}/Auth`;
  private readonly tokenKey = 'auth_token';

  register(payload: RegisterUser): Observable<void> {
    // Multipart is needed if profilePicture is a File
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => {
      if (value) formData.append(key, value);
    });
    return this.http.post<void>(`${this.baseUrl}/register`, formData);
  }

  login(credentials: Login): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, credentials).pipe(
      tap(response => this.saveToken(response.token))
    );
  }

  logout(): Observable<void> {
    // Clear the token immediately for a responsive user experience.
    this.clearToken();
    // The server call is now for backend session cleanup.
    // The client is already logged out.
    return this.http.post<void>(`${this.baseUrl}/logout`, {});
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  private saveToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
  }

  private clearToken(): void {
    localStorage.removeItem(this.tokenKey);
  }
}
