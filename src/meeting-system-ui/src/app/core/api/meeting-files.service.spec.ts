import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';

import { MeetingFilesService } from './meeting-files.service';
import { API_CONFIG } from '../config/api.config';
import { AppFile, PresignedUrl } from '../models/file.model';

describe('MeetingFilesService', () => {
  let service: MeetingFilesService;
  let httpController: HttpTestingController;
  const baseUrl = `/api/meetings`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        MeetingFilesService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api' } }
      ]
    });

    service = TestBed.inject(MeetingFilesService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  describe('getFiles', () => {
    it('should retrieve all files for a meeting', () => {
      const meetingId = 'meeting-123';
      const mockFiles: AppFile[] = [
        {
          id: 'file-1',
          fileName: 'document.pdf',
          contentType: 'application/pdf',
          sizeBytes: 1024000
        },
        {
          id: 'file-2',
          fileName: 'presentation.pptx',
          contentType: 'application/vnd.ms-powerpoint',
          sizeBytes: 2048000
        }
      ];

      service.getFiles(meetingId).subscribe(files => {
        expect(files).toEqual(mockFiles);
        expect(files.length).toBe(2);
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files`);
      expect(req.request.method).toBe('GET');
      req.flush(mockFiles);
    });

    it('should return empty array when no files exist', () => {
      const meetingId = 'meeting-123';

      service.getFiles(meetingId).subscribe(files => {
        expect(files).toEqual([]);
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files`);
      req.flush([]);
    });
  });

  describe('uploadFiles', () => {
    it('should upload multiple files', () => {
      const meetingId = 'meeting-123';
      const mockFiles = [
        new File(['content1'], 'file1.txt', { type: 'text/plain' }),
        new File(['content2'], 'file2.txt', { type: 'text/plain' })
      ];

      const mockResponse: AppFile[] = [
        {
          id: 'file-1',
          fileName: 'file1.txt',
          contentType: 'text/plain',
          sizeBytes: 8
        },
        {
          id: 'file-2',
          fileName: 'file2.txt',
          contentType: 'text/plain',
          sizeBytes: 8
        }
      ];

      service.uploadFiles(meetingId, mockFiles).subscribe(files => {
        expect(files).toEqual(mockResponse);
        expect(files.length).toBe(2);
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body instanceof FormData).toBeTruthy();
      req.flush(mockResponse);
    });

    it('should handle single file upload', () => {
      const meetingId = 'meeting-123';
      const mockFile = [new File(['content'], 'document.pdf', { type: 'application/pdf' })];

      const mockResponse: AppFile[] = [{
        id: 'file-1',
        fileName: 'document.pdf',
        contentType: 'application/pdf',
        sizeBytes: 7
      }];

      service.uploadFiles(meetingId, mockFile).subscribe(files => {
        expect(files.length).toBe(1);
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files`);
      req.flush(mockResponse);
    });
    
    // Add test for upload with 413 Content Too Large response
    it('should handle file upload failure with content too large', () => {
      const meetingId = 'meeting-123';
      const mockFiles = [new File(['large-content'], 'large-file.pdf', { type: 'application/pdf' })];

      service.uploadFiles(meetingId, mockFiles).subscribe({
        error: (error) => {
          expect(error.status).toBe(413);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files`);
      req.flush({ message: 'File too large' }, { status: 413, statusText: 'Content Too Large' });
    });
  });

  describe('getDownloadUrl', () => {
    it('should retrieve presigned download URL', () => {
      const meetingId = 'meeting-123';
      const fileId = 'file-456';
      const mockUrl: PresignedUrl = {
        url: 'https://s3.amazonaws.com/bucket/file?signature=xyz'
      };

      service.getDownloadUrl(meetingId, fileId).subscribe(presignedUrl => {
        expect(presignedUrl).toEqual(mockUrl);
        expect(presignedUrl.url).toContain('s3.amazonaws.com');
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files/${fileId}/download-url`);
      expect(req.request.method).toBe('GET');
      req.flush(mockUrl);
    });

    it('should handle missing file error', () => {
      const meetingId = 'meeting-123';
      const fileId = 'non-existent';

      service.getDownloadUrl(meetingId, fileId).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(404);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files/${fileId}/download-url`);
      req.flush({ message: 'File not found' }, { status: 404, statusText: 'Not Found' });
    });
  });

  describe('deleteFile', () => {
    it('should delete a file', () => {
      const meetingId = 'meeting-123';
      const fileId = 'file-456';

      service.deleteFile(meetingId, fileId).subscribe(response => {
        expect(response).toBeNull();
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files/${fileId}`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });

    it('should handle delete error', () => {
      const meetingId = 'meeting-123';
      const fileId = 'file-456';

      service.deleteFile(meetingId, fileId).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(403);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/files/${fileId}`);
      req.flush({ message: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
    });
  });
});
