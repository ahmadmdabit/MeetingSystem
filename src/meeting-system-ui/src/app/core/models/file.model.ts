export interface AppFile {
  id: string;
  fileName?: string;
  contentType?: string;
  sizeBytes: number;
}

export interface PresignedUrl {
  url?: string;
}
