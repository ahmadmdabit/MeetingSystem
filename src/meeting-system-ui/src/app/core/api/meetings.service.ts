import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { AddParticipant, CreateMeeting, Meeting, UpdateMeeting } from '../models/meeting.model';

@Injectable({ providedIn: 'root' })
export class MeetingsService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly baseUrl = `${this.config.baseUrl}/meetings`;

  getMeetings(): Observable<Meeting[]> {
    return this.http.get<Meeting[]>(this.baseUrl);
  }

  createMeeting(payload: CreateMeeting): Observable<Meeting> {
    return this.http.post<Meeting>(this.baseUrl, payload);
  }

  getMeetingById(id: string): Observable<Meeting> {
    return this.http.get<Meeting>(`${this.baseUrl}/${id}`);
  }

  updateMeeting(id: string, payload: UpdateMeeting): Observable<Meeting> {
    return this.http.put<Meeting>(`${this.baseUrl}/${id}`, payload);
  }

  cancelMeeting(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  addParticipant(meetingId: string, payload: AddParticipant): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${meetingId}/participants`, payload);
  }

  removeParticipant(meetingId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${meetingId}/participants/${userId}`);
  }
}
