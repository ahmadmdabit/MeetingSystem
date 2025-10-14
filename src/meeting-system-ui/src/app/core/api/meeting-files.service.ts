import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { AppFile, PresignedUrl } from '../models/file.model';

@Injectable({ providedIn: 'root' })
export class MeetingFilesService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly baseUrl = `${this.config.baseUrl}/meetings`;

  getFiles(meetingId: string): Observable<AppFile[]> {
    return this.http.get<AppFile[]>(`${this.baseUrl}/${meetingId}/files`);
  }

  uploadFiles(meetingId: string, files: File[]): Observable<AppFile[]> {
    const formData = new FormData();
    files.forEach(file => formData.append('files', file, file.name));
    return this.http.post<AppFile[]>(`${this.baseUrl}/${meetingId}/files`, formData);
  }

  getDownloadUrl(meetingId: string, fileId: string): Observable<PresignedUrl> {
    return this.http.get<PresignedUrl>(`${this.baseUrl}/${meetingId}/files/${fileId}/download-url`);
  }

  deleteFile(meetingId: string, fileId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${meetingId}/files/${fileId}`);
  }
}
