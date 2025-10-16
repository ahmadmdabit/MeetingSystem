export interface AppFile {
  id: string;
  fileName?: string;
  contentType?: string;
  sizeBytes: number;
  uploadedByUserId: string;
}

export interface PresignedUrl {
  url?: string;
}
