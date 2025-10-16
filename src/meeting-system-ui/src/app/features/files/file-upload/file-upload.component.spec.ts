import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FileUploadComponent } from './file-upload.component';
import { MeetingFilesService } from '../../../core/api/meeting-files.service';
import { of, throwError, delay, Subject } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { AppFile } from '../../../core/models/file.model';

// Helper function to create a mock FileList
const createMockFileList = (files: File[]): FileList => {
  const dataTransfer = new DataTransfer();
  files.forEach(file => dataTransfer.items.add(file));
  return dataTransfer.files;
};

// Use a Subject for controlled asynchronous testing
class MockMeetingFilesService {
  uploadFilesSource = new Subject<AppFile[]>();
  uploadFiles(meetingId: string, files: File[]) {
    return this.uploadFilesSource.asObservable();
  }
}

describe('FileUploadComponent', () => {
  let component: FileUploadComponent;
  let fixture: ComponentFixture<FileUploadComponent>;
  let mockMeetingFilesService: MockMeetingFilesService;

  beforeEach(async () => {
    mockMeetingFilesService = new MockMeetingFilesService();

    await TestBed.configureTestingModule({
      imports: [FileUploadComponent],
      providers: [
        provideZonelessChangeDetection(),
        { provide: MeetingFilesService, useValue: mockMeetingFilesService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FileUploadComponent);
    component = fixture.componentInstance;
    component.meetingId = 'meeting-123';
    // FIX: Do NOT call detectChanges() here.
  });

  it('should create', () => {
    fixture.detectChanges(); // Call detectChanges for this specific test.
    expect(component).toBeTruthy();
  });

  it('should upload files, reset state, and emit success event on success', (done) => {
    // Arrange
    fixture.detectChanges(); // Initial render
    const mockFile1 = new File(['content1'], 'file1.txt', { type: 'text/plain' });
    component.selectedFiles = [mockFile1];
    const uploadSpy = spyOn(mockMeetingFilesService, 'uploadFiles').and.callThrough();
    const emitSpy = spyOn(component.uploadSuccess, 'emit');

    // Act
    const upload$ = component.uploadFiles();
    expect(upload$).not.toBeNull();

    upload$!.subscribe({
      next: () => {
        // Assert
        expect(component.uploading).toBe(false);
        expect(component.uploadProgress).toBe(100);
        expect(component.selectedFiles.length).toBe(0);
        expect(emitSpy).toHaveBeenCalled();
        done();
      }
    });

    // Assert 1: Check the "uploading" state
    expect(component.uploading).toBe(true);
    expect(component.uploadProgress).toBe(50);
    expect(uploadSpy).toHaveBeenCalledWith('meeting-123', [mockFile1]);

    // Act 2: Simulate the successful server response
    mockMeetingFilesService.uploadFilesSource.next([]);
    mockMeetingFilesService.uploadFilesSource.complete();
  });

  it('should stop progress and show error message on upload failure', (done) => {
    // Arrange
    fixture.detectChanges(); // Initial render
    const mockFile = new File(['content'], 'file.txt', { type: 'text/plain' });
    component.selectedFiles = [mockFile];
    const errorResponse = { error: { message: 'Upload failed' } };

    // Act
    const upload$ = component.uploadFiles();
    expect(upload$).not.toBeNull();

    upload$!.subscribe({
      error: (err) => {
        // Assert
        expect(component.uploading).toBe(false);
        expect(component.error).toBe('Upload failed');
        expect(component.uploadProgress).toBe(50);
        done();
      }
    });

    // Assert 1: Check the intermediate state
    expect(component.uploading).toBe(true);
    expect(component.uploadProgress).toBe(50);

    // Act 2: Simulate the server error response
    mockMeetingFilesService.uploadFilesSource.error(errorResponse);
    mockMeetingFilesService.uploadFilesSource.complete();
  });

  it('should format file size correctly', () => {
    fixture.detectChanges();
    // FIX: Update assertions to match the toFixed(2) output.
    expect(component.formatFileSize(0)).toBe('0 Bytes');
    expect(component.formatFileSize(1024)).toBe('1.00 KB');
    expect(component.formatFileSize(1048576)).toBe('1.00 MB');
    expect(component.formatFileSize(1073741824)).toBe('1.00 GB');
    expect(component.formatFileSize(1536)).toBe('1.50 KB');
  });

  it('should not call upload service when no files are selected', () => {
    fixture.detectChanges();
    const uploadSpy = spyOn(mockMeetingFilesService, 'uploadFiles').and.callThrough();
    component.selectedFiles = [];
    component.uploadFiles();
    expect(uploadSpy).not.toHaveBeenCalled();
    expect(component.uploading).toBeFalse();
  });

  it('should prevent adding duplicate files (same name and size)', () => {
    const f1 = new File(['abc'], 'dup.txt', { type: 'text/plain' });
    const f2 = new File(['abc'], 'dup.txt', { type: 'text/plain' });
    const files = createMockFileList([f1, f2]);
    component.addFiles(files);
    expect(component.selectedFiles.length).toBe(1);
    expect(component.selectedFiles[0].name).toBe('dup.txt');
  });

  it('should remove a file from the selection', () => {
    const f1 = new File(['a'], 'a.txt', { type: 'text/plain' });
    const f2 = new File(['b'], 'b.txt', { type: 'text/plain' });
    component.selectedFiles = [f1, f2];
    component.removeFile(f1);
    expect(component.selectedFiles).toEqual([f2]);
  });

  it('should toggle drag-over state and add files on drop', () => {
    const dragEvent = {
      preventDefault: jasmine.createSpy('preventDefault'),
      stopPropagation: jasmine.createSpy('stopPropagation'),
      dataTransfer: { files: createMockFileList([new File(['x'], 'x.txt', { type: 'text/plain' })]) }
    } as unknown as DragEvent;

    component.onDragOver(dragEvent);
    expect(dragEvent.preventDefault).toHaveBeenCalled();
    expect(component.isDragOver).toBeTrue();

    component.onDragLeave(dragEvent);
    expect(dragEvent.preventDefault).toHaveBeenCalled();
    expect(dragEvent.stopPropagation).toHaveBeenCalled();
    expect(component.isDragOver).toBeFalse();

    component.onDrop(dragEvent);
    expect(dragEvent.preventDefault).toHaveBeenCalled();
    expect(dragEvent.stopPropagation).toHaveBeenCalled();
    expect(component.isDragOver).toBeFalse();
    expect(component.selectedFiles.some(file => file.name === 'x.txt')).toBeTrue();
  });

  it('should set a generic error message and stop uploading when server error has no message', (done) => {
    fixture.detectChanges();
    const f = new File(['x'], 'x.txt', { type: 'text/plain' });
    component.selectedFiles = [f];
    
    const upload$ = component.uploadFiles();
    expect(upload$).not.toBeNull();

    upload$!.subscribe({
      error: () => {
        expect(component.uploading).toBeFalse();
        expect(component.error).toBe('Failed to upload files');
        expect(component.uploadProgress).toBe(50);
        done();
      }
    });

    mockMeetingFilesService.uploadFilesSource.error({});
    mockMeetingFilesService.uploadFilesSource.complete();
  });
});
