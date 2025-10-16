import { jwtDecode } from 'jwt-decode';
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { catchError, Observable, of, tap } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { AuthResponse, Login, RegisterUser } from '../models/auth.model';
import { JwtPayload } from '../models/jwt-payload.model';

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
    // 1. Get the token BEFORE you clear it.
    const token = this.getToken();

    // 2. Clear the token immediately for instant UX feedback.
    this.clearToken();

    // 3. If there was no token to begin with, there's nothing to do on the server.
    if (!token) {
      return of(undefined); // Return a completed observable.
    }

    // 4. Manually create the Authorization header with the token we saved.
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`
    });

    // 5. Make the API call with the explicit headers, bypassing the interceptor's logic.
    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { headers }).pipe(
      // 6. Gracefully handle errors. If the API call fails (e.g., network down),
      // the user is already logged out on the client. We don't want to show an error.
      catchError(error => {
        console.error('Logout API call failed, but user is logged out on client.', error);
        return of(undefined); // Swallow the error and complete the stream.
      })
    );
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  // FIX: Refine this method to return a clean, mapped object
  public getCurrentUser(): { id: string; email: string; roles: string[] } | null {
    const token = this.getToken();
    if (!token) {
      return null;
    }
    try {
      const decoded = jwtDecode<JwtPayload>(token);

      // Check if the token is expired. exp is in seconds, Date.now() is in milliseconds.
      if (decoded.exp * 1000 < Date.now()) {
        console.error("JWT has expired.");
        return null;
      }

      const roleClaim = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

      return {
        id: decoded.sub, // Map 'sub' to 'id'
        email: decoded.email,
        // Ensure roles are always returned as an array
        roles: Array.isArray(roleClaim) ? roleClaim : [roleClaim],
      };
    } catch (error) {
      console.error("Failed to decode JWT", error);
      return null;
    }
  }

  // FIX: Update isAdmin to use the new, reliable getCurrentUser method
  public isAdmin(): boolean {
    const user = this.getCurrentUser();
    return user?.roles.includes('Admin') ?? false;
  }

  private saveToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
  }

  private clearToken(): void {
    localStorage.removeItem(this.tokenKey);
  }
}
