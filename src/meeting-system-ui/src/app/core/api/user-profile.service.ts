import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { UpdateUserProfile, UserProfile } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserProfileService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly baseUrl = `${this.config.baseUrl}/users/me`;

  getMe(): Observable<UserProfile> {
    return this.http.get<UserProfile>(this.baseUrl);
  }

  updateMe(payload: UpdateUserProfile): Observable<void> {
    return this.http.put<void>(this.baseUrl, payload);
  }

  uploadProfilePicture(file: File): Observable<void> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.put<void>(`${this.baseUrl}/profile-picture`, formData);
  }

  deleteProfilePicture(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/profile-picture`);
  }
}
