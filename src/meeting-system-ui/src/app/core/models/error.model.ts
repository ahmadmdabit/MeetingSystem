export interface ApiError {
  message: string;
}

export interface ApiErrorDetailed {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  [key: string]: any; // For additional properties
}

export interface ApiErrorDetailedValidation extends ApiErrorDetailed {
  errors: { [key: string]: string[] };
}

/**
 * Type guard to safely check if an error object matches the ApiError shape.
 */
export function isApiError(error: any): error is ApiError {
  return error && typeof error.message === 'string';
}

/**
 * Type guard to safely check if an error object matches the ApiErrorDetailed shape.
 */
export function isApiErrorDetailed(error: any): error is ApiErrorDetailed {
  return error && (typeof error.title === 'string' || typeof error.detail === 'string');
}

/**
 * Type guard to safely check if an error object matches the ApiErrorDetailedValidation shape.
 */
export function isApiErrorDetailedValidation(error: any): error is ApiErrorDetailedValidation {
  return error && error.status === 400 && error.errors && typeof error.errors === 'object';
}
