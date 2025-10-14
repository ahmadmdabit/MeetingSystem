import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { AssignRole, UserProfile } from '../models/user.model';
import { PresignedUrl } from '../models/file.model';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly baseUrl = `${this.config.baseUrl}/users`;

  getUsers(): Observable<UserProfile[]> {
    return this.http.get<UserProfile[]>(this.baseUrl);
  }

  getUserProfilePictureUrl(userId: string): Observable<PresignedUrl> {
    return this.http.get<PresignedUrl>(`${this.baseUrl}/${userId}/profile-picture`);
  }

  assignRole(userId: string, payload: AssignRole): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${userId}/roles`, payload);
  }

  removeRole(userId: string, roleName: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${userId}/roles/${roleName}`);
  }
}
