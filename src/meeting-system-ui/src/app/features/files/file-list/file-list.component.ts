import { Component, inject, Input, ChangeDetectionStrategy } from '@angular/core';
import { AuthService } from '../../../core/api/auth.service';
import { MeetingFilesService } from '../../../core/api/meeting-files.service';
import { AppFile } from '../../../core/models/file.model';
import { AsyncPipe } from '@angular/common';
import { Observable, BehaviorSubject, switchMap } from 'rxjs';

@Component({
  selector: 'app-file-list',
  standalone: true,
  imports: [AsyncPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="file-list-container">
      @if (files$ | async; as files) {
        <h4>Attached Files ({{ files.length }})</h4>
        @if (files.length > 0) {
          <div class="files-grid">
            @for (file of files; track file.id) {
              <div class="file-item">
                <div class="file-icon">{{ getFileIcon(file.contentType) }}</div>
                <div class="file-info">
                  <div class="file-name">{{ file.fileName }}</div>
                  <div class="file-meta">
                    <span class="file-type">{{ getFileType(file.contentType) }}</span>
                    <span class="file-size">{{ formatFileSize(file.sizeBytes) }}</span>
                  </div>
                </div>
                <div class="file-actions">
                  <button type="button" class="btn btn-outline" (click)="downloadFile(file.id)">Download</button>
                  <!-- NEW: Wrap the delete button in a conditional @if block -->
                  @if (canDeleteFiles && canDelete(file)) {
                    <button type="button" class="btn btn-danger" (click)="deleteFile(file.id)">Delete</button>
                  }
                </div>
              </div>
            }
          </div>
        } @else {
          <p class="no-files">No files attached to this meeting.</p>
        }
      } @else {
        <div class="loading">Loading files ⌛</div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      margin-top: 2rem; /* var(--spacing-lg) */
    }

    h4 {
      margin-top: 0;
      margin-bottom: 1rem; /* var(--spacing-base) */
      color: var(--text-color-dark, #333);
      padding: var(--spacing-sm);
      background-color: var(--card-border-color);
      border-radius: var(--border-radius);
    }

    .files-grid {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .file-item {
      display: flex;
      align-items: center;
      padding: 1rem; /* var(--spacing-base) */
      background: var(--background-color-white, white);
      border-radius: 8px;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
    }

    .file-icon {
      font-size: 1.5rem;
      margin-right: 1rem; /* var(--spacing-base) */
      width: 40px;
      height: 40px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--background-color-light, #f8f9fa);
      border-radius: 4px;
    }

    .file-info {
      flex: 1;
      min-width: 0; /* Prevents long file names from breaking layout */
    }

    .file-name {
      font-weight: 500;
      color: var(--text-color-dark, #333);
      margin-bottom: 0.25rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .file-meta {
      display: flex;
      gap: 1rem; /* var(--spacing-base) */
      font-size: 0.875rem; /* var(--font-size-sm) */
      color: var(--text-color-muted, #666);
    }

    .file-type {
      text-transform: uppercase;
    }

    .file-actions {
      display: flex;
      gap: 0.5rem; /* var(--spacing-sm) */
    }

    .no-files {
      text-align: center;
      padding: 1rem; /* var(--spacing-base) */
      color: var(--text-color-muted, #666);
      font-style: italic;
    }

    /*
     * All styles for .btn, .btn-outline, and .btn-danger are now
     * inherited from the global styles.scss and have been removed.
     */
  `]
})
export class FileListComponent {
  @Input({ required: true }) meetingId!: string;
  // Add new inputs for the required IDs
  @Input({ required: true }) organizerId!: string;
  @Input() canDeleteFiles: boolean = true;

  private meetingFilesService = inject(MeetingFilesService);
  // Inject AuthService to check for admin role
  private authService = inject(AuthService);
  private readonly currentUserId = this.authService.getCurrentUser()?.id;
  private readonly isAdmin = this.authService.isAdmin();

  // FIX: Implement a reactive refresh pattern
  private refresh$ = new BehaviorSubject<void>(undefined);

  public refresh(): void {
    this.refresh$.next();
  }

  files$: Observable<AppFile[]> = this.refresh$.pipe(
    switchMap(() => this.meetingFilesService.getFiles(this.meetingId))
  );

  // Add the logic to determine if the delete button should be shown
  canDelete(file: AppFile): boolean {
    if (this.isAdmin) {
      return true;
    }
    // This logic will now work correctly
    if (this.currentUserId && this.currentUserId === this.organizerId) {
      return true;
    }
    return this.currentUserId === file.uploadedByUserId;
  }

  deleteFile(fileId: string): void {
    if (confirm('Are you sure you want to delete this file?')) {
      this.meetingFilesService.deleteFile(this.meetingId, fileId).subscribe({
        complete: () => this.refresh$.next()
      });
    }
  }

  downloadFile(fileId: string): void {
    this.meetingFilesService.getDownloadUrl(this.meetingId, fileId).subscribe({
      next: response => {
        if (response.url) {
          const link = document.createElement('a');
          link.href = response.url;
          link.target = '_blank';
          link.download = '';
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
        }
      },
      error: err => console.error('Error getting download URL:', err)
    });
  }

  // --- Helper Methods ---
  getFileIcon(contentType: string | undefined): string {
    if (!contentType) return '📄';
    if (contentType.includes('image')) return '🖼️';
    if (contentType.includes('pdf')) return '📑';
    if (contentType.includes('word')) return '📝';
    if (contentType.includes('excel')) return '📊';
    if (contentType.includes('powerpoint')) return '🎬';
    if (contentType.includes('audio')) return '🎵';
    if (contentType.includes('video')) return '📹';
    if (contentType.includes('zip')) return '📦';
    return '📄';
  }

  getFileType(contentType: string | undefined): string {
    if (!contentType) return 'FILE';
    const mapping: { [key: string]: string } = {
      image: 'IMAGE',
      pdf: 'PDF',
      word: 'DOC',
      excel: 'XLS',
      powerpoint: 'PPT',
      audio: 'AUDIO',
      video: 'VIDEO',
      zip: 'ARCHIVE',
      compressed: 'ARCHIVE'
    };
    for (const key in mapping) {
      if (contentType.includes(key)) return mapping[key];
    }
    return contentType.split('/')[1]?.toUpperCase() || 'FILE';
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return (bytes / Math.pow(k, i)).toFixed(2) + ' ' + sizes[i];
  }
}
