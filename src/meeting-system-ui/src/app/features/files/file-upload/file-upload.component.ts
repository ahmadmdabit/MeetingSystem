import { Component, inject, Input, Output, EventEmitter, ChangeDetectorRef } from '@angular/core';
import { MeetingFilesService } from '../../../core/api/meeting-files.service';
import { DecimalPipe } from '@angular/common'; // FIX: Import DecimalPipe
import { Observable, tap } from 'rxjs';
import { AppFile } from '../../../core/models/file.model';

@Component({
  selector: 'app-file-upload',
  standalone: true,
  // FIX: Use DecimalPipe for number formatting. No CommonModule needed with @if/@for.
  imports: [DecimalPipe],
  template: `
    <div class="file-upload-container">
      <div
        class="upload-area"
        [class.drag-over]="isDragOver"
        (dragover)="onDragOver($event)"
        (dragleave)="onDragLeave($event)"
        (drop)="onDrop($event)"
      >
        <div class="upload-content" (click)="$event.stopPropagation()">
          <div class="upload-icon">📁</div>
          <p class="upload-text">Drag & drop files here or click to browse</p>
          <p class="upload-hint">Supported formats: PDF, DOC, XLS, PPT, images, etc.</p>
          <input
            id="fileInput"
            type="file"
            #fileInput
            (change)="onFileSelected($event)"
            multiple
            style="position:absolute; left:-9999px;opacity: 0;"
          />
          <label for="fileInput" (click)="$event.stopPropagation()" class="btn btn-primary">Browse Files</label>
        </div>
      </div>

      <!-- FIX: Use modern @if syntax -->
      @if (uploading) {
        <div class="upload-progress">
          <div class="progress-bar">
            <div
              class="progress-fill"
              [style.width.%]="uploadProgress"
            ></div>
          </div>
          <p class="progress-text">Uploading ⌛ {{ uploadProgress | number:'1.0-0' }}%</p>
        </div>
      }

      @if (error) {
        <div class="alert alert-danger mt-3">
          {{ error }}
        </div>
      }

      @if (selectedFiles.length > 0) {
        <div class="selected-files">
          <h5>Files to upload:</h5>
          <ul class="files-list">
            <!-- FIX: Use modern @for syntax with track -->
            @for (file of selectedFiles; track file.name + file.size) {
              <li class="file-item">
                <span class="file-name">{{ file.name }}</span>
                <span class="file-size">{{ formatFileSize(file.size) }}</span>
                <button type="button" class="btn btn-sm btn-danger" (click)="removeFile(file)">Remove</button>
              </li>
            }
          </ul>
          <button
            type="button"
            class="btn btn-success"
            [disabled]="selectedFiles.length === 0 || uploading || !meetingId"
            (click)="startUpload()"
          >
            Upload Files
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      margin-top: var(--spacing-base);
    }

    .upload-area {
      border: 2px dashed var(--border-color, #ccc);
      border-radius: 8px;
      padding: var(--spacing-lg);
      text-align: center;
      transition: border-color 0.3s ease, background-color 0.3s ease;
    }

    .upload-area:hover,
    .upload-area.drag-over {
      border-color: var(--primary-color, #007bff);
      background-color: var(--background-color-light, #f8f9fa);
    }

    .upload-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--spacing-sm);
    }

    .upload-icon {
      font-size: 3rem;
    }

    .upload-text {
      font-size: 1.1rem;
      margin: 0;
      color: var(--text-color-light, #555);
    }

    .upload-hint {
      color: var(--text-color-muted, #888);
      margin-bottom: var(--spacing-base);
    }

    .upload-progress {
      margin-top: var(--spacing-base);
    }

    .progress-bar {
      height: 20px;
      background-color: #e9ecef;
      border-radius: 10px;
      overflow: hidden;
    }

    .progress-fill {
      height: 100%;
      background-color: var(--primary-color, #007bff);
      transition: width 0.3s ease;
    }

    .progress-text {
      text-align: center;
      margin-top: var(--spacing-sm);
      font-weight: 500;
    }

    .selected-files h5 {
      margin-top: 1.5rem;
      margin-bottom: var(--spacing-sm);
      color: var(--text-color-dark, #333);
    }

    .files-list {
      list-style: none;
      padding: 0;
      margin: var(--spacing-sm) 0 var(--spacing-base) 0;
      border: 1px solid var(--border-color, #ccc);
      border-radius: 4px;
    }

    .file-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem;
      border-bottom: 1px solid var(--border-color, #eee);
    }

    .file-item:last-child {
      border-bottom: none;
    }

    .file-name {
      flex: 1;
      font-weight: 500;
      margin-right: var(--spacing-base);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .file-size {
      color: var(--text-color-muted, #666);
      margin-right: var(--spacing-base);
    }

    .mt-3 {
      margin-top: var(--spacing-base);
    }

    /*
     * All styles for .btn, .alert, and their variants have been removed
     * as they are now provided by the global styles.scss.
     */
  `]
})
export class FileUploadComponent {
  @Input({ required: true }) meetingId!: string;
  // NEW: Add a disabled input
  @Input() disabled: boolean = false;
  // FIX: Add Output event to notify parent on successful upload
  @Output() uploadSuccess = new EventEmitter<void>();

  private meetingFilesService = inject(MeetingFilesService);
  // NEW: Inject ChangeDetectorRef
  private cdr = inject(ChangeDetectorRef);

  selectedFiles: File[] = [];
  uploading = false;
  uploadProgress = 0;
  error: string | null = null;
  isDragOver = false;

  onFileSelected(event: Event) {
    const files: FileList | null = (event.target as HTMLInputElement).files;
    if (files) {
      this.addFiles(files);
    }
    // Clear the input value to allow selecting the same file again
    (event.target as HTMLInputElement).value = '';
  }

  addFiles(files: FileList) {
    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      if (!this.isFileSelected(file)) {
        this.selectedFiles.push(file);
      }
    }
  }

  isFileSelected(file: File): boolean {
    // Check by name and size to prevent duplicates
    return this.selectedFiles.some(f => f.name === file.name && f.size === file.size);
  }

  removeFile(file: File) {
    this.selectedFiles = this.selectedFiles.filter(f => f !== file);
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;

    if (event.dataTransfer?.files) {
      this.addFiles(event.dataTransfer.files);
    }
  }

  // NEW: Make uploadFiles public and return the observable
  public uploadFiles(): Observable<AppFile[]> | null {
    if (this.selectedFiles.length === 0 || this.disabled) {
      return null;
    }
    if (!this.meetingId) {
      this.error = 'Create the meeting first to upload files';
      this.cdr.detectChanges();
      return null;
    }

    this.uploading = true;
    this.error = null;
    this.uploadProgress = 50;
    this.cdr.detectChanges(); // Manually trigger change detection for immediate feedback

    return this.meetingFilesService.uploadFiles(this.meetingId, this.selectedFiles).pipe(
      tap({
        next: () => {
          this.uploadProgress = 100;
          this.uploading = false;
          this.selectedFiles = [];
          this.uploadSuccess.emit();
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.uploading = false;
          this.error = err.error?.message || 'Failed to upload files';
          this.cdr.detectChanges();
        }
      })
    );
  }

  public startUpload(): void {
    const upload$ = this.uploadFiles();
    if (upload$) {
      upload$.subscribe();
    }
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    // FIX: Remove the parseFloat call. toFixed(2) already returns the correct string.
    return (bytes / Math.pow(k, i)).toFixed(2) + ' ' + sizes[i];
  }
}
