import { InjectionToken } from '@angular/core';

export interface ApiConfig {
  baseUrl: string;
}

export const API_CONFIG = new InjectionToken<ApiConfig>('api.config');

export const DEFAULT_API_CONFIG: ApiConfig = {
  baseUrl: '/api' // Default development URL
};
