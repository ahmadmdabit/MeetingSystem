import { API_CONFIG, DEFAULT_API_CONFIG } from '../../../core/config/api.config';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FileListComponent } from './file-list.component';
import { MeetingFilesService } from '../../../core/api/meeting-files.service';
import { firstValueFrom, Observable, of } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { AppFile, PresignedUrl } from '../../../core/models/file.model';

// Mocks
const MOCK_FILES: AppFile[] = [
  { id: 'file-1', fileName: 'document.pdf', contentType: 'application/pdf', sizeBytes: 102400, uploadedByUserId: 'user-1' },
  { id: 'file-2', fileName: 'image.png', contentType: 'image/png', sizeBytes: 204800, uploadedByUserId: 'user-1' }
];

class MockMeetingFilesService {
  getFiles(meetingId: string): Observable<AppFile[]> {
    return of(MOCK_FILES);
  }
  getDownloadUrl(meetingId: string, fileId: string): Observable<PresignedUrl> {
    return of({ url: 'https://example.com/download' });
  }
  deleteFile(meetingId: string, fileId: string): Observable<void> {
    return of(undefined);
  }
}

describe('FileListComponent', () => {
  let component: FileListComponent;
  let fixture: ComponentFixture<FileListComponent>;
  let mockMeetingFilesService: MockMeetingFilesService;

  beforeEach(async () => {
    mockMeetingFilesService = new MockMeetingFilesService();

    await TestBed.configureTestingModule({
      imports: [FileListComponent, HttpClientTestingModule],
      providers: [
        provideZonelessChangeDetection(),
        { provide: MeetingFilesService, useValue: mockMeetingFilesService },
        { provide: API_CONFIG, useValue: DEFAULT_API_CONFIG }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FileListComponent);
    component = fixture.componentInstance;
    component.meetingId = 'meeting-123';
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  describe('Initialization and Display', () => {
    it('should load files on initialization', async () => {
      const getFilesSpy = spyOn(mockMeetingFilesService, 'getFiles').and.callThrough();
      fixture.detectChanges();
      await fixture.whenStable();

      const files = await firstValueFrom(component.files$);

      expect(getFilesSpy).toHaveBeenCalledWith('meeting-123');
      expect(files).toEqual(MOCK_FILES);
    });
  });

  describe('User Actions', () => {
    // FIX: Remove the nested beforeEach that calls detectChanges prematurely.
    // Each test will now control its own change detection.

    it('should get a download URL and attempt to trigger a download', () => {
      fixture.detectChanges(); // Render the component for this test.
      const getUrlSpy = spyOn(mockMeetingFilesService, 'getDownloadUrl').and.callThrough();
      const mockLink = { href: '', target: '', download: '', click: jasmine.createSpy('click') };
      spyOn(document, 'createElement').and.returnValue(mockLink as any);
      spyOn(document.body, 'appendChild').and.stub();
      spyOn(document.body, 'removeChild').and.stub();

      component.downloadFile('file-1');

      expect(getUrlSpy).toHaveBeenCalledWith('meeting-123', 'file-1');
      expect(mockLink.href).toBe('https://example.com/download');
      expect(mockLink.target).toBe('_blank');
      expect(mockLink.click).toHaveBeenCalled();
    });

    it('should delete a file and refresh the list on confirmation', async () => {
      spyOn(window, 'confirm').and.returnValue(true);
      const deleteSpy = spyOn(mockMeetingFilesService, 'deleteFile').and.callThrough();
      const getFilesSpy = spyOn(mockMeetingFilesService, 'getFiles').and.callThrough();

      fixture.detectChanges();
      await fixture.whenStable();
      // The initial load should have happened.
      expect(getFilesSpy).toHaveBeenCalledTimes(1);

      component.deleteFile('file-1');
      await fixture.whenStable();

      expect(deleteSpy).toHaveBeenCalledWith('meeting-123', 'file-1');
      // The refresh mechanism should have triggered a second call.
      expect(getFilesSpy).toHaveBeenCalledTimes(2);
    });

    it('should not delete a file if the user cancels confirmation', () => {
      fixture.detectChanges();
      spyOn(window, 'confirm').and.returnValue(false);
      const deleteSpy = spyOn(mockMeetingFilesService, 'deleteFile');
      component.deleteFile('file-1');
      expect(deleteSpy).not.toHaveBeenCalled();
    });
  });

  describe('Helper Methods', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should format file size correctly', () => {
      // FIX: Update assertions to match the toFixed(2) output.
      expect(component.formatFileSize(0)).toBe('0 Bytes');
      expect(component.formatFileSize(1024)).toBe('1.00 KB');
      expect(component.formatFileSize(1536)).toBe('1.50 KB');
    });

    it('should determine correct file icon based on content type', () => {
      expect(component.getFileIcon('image/png')).toBe('🖼️');
      expect(component.getFileIcon('application/pdf')).toBe('📑');
      expect(component.getFileIcon(undefined)).toBe('📄');
    });

    it('should determine correct file type based on content type', () => {
      expect(component.getFileType('image/png')).toBe('IMAGE');
      expect(component.getFileType('application/pdf')).toBe('PDF');
      expect(component.getFileType('text/plain')).toBe('PLAIN');
    });
  });
});
