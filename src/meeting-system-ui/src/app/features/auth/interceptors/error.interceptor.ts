import {
  HttpInterceptorFn,
  HttpErrorResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../../../core/api/auth.service'; // Adjust path as needed
import { isApiError, isApiErrorDetailed, isApiErrorDetailedValidation } from '../../../core/models/error.model'; // Adjust path as needed


/**
 * Parses an HttpErrorResponse to extract a single, user-friendly error message.
 */
function getErrorMessage(error: HttpErrorResponse): string {
  // Default message for unknown errors
  const defaultMessage = 'An unexpected error occurred. Please try again later.';

  if (error.status === 0 || error.error instanceof ErrorEvent) {
    // A client-side or network error occurred.
    return 'Could not connect to the server. Please check your network connection.';
  }

  // The backend returned an unsuccessful response code.
  // The response body may contain clues as to what went wrong.
  const errorBody = error.error;

  // Priority 1: Check for our custom ApiError shape: { message: "..." }
  if (isApiError(errorBody)) {
    return errorBody.message;
  }

  // Priority 2: Check for the standard ApiErrorDetailed shape: { title: "...", detail: "..." }
  if (isApiErrorDetailed(errorBody)) {
    // Prefer the 'detail' field if it exists, otherwise use 'title'.
    return errorBody.detail || errorBody.title || defaultMessage;
  }

  // Priority 3: Handle ASP.NET Core framework validation errors
  if (error.status === 400 && errorBody?.errors) {
    // Grab the first error from the first field.
    const validationErrors = Object.values(errorBody.errors).flat();
    return (validationErrors[0] as string) || 'The submitted data is invalid.';
  }

  // Fallback to the status text if no parsable body is available
  return error.statusText || defaultMessage;
}


export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {

      // --- Step 1: Handle critical, non-recoverable errors first ---

      // Highest priority: Handle 401 Unauthorized by logging out.
      // This is a terminal action for the user's session.
      if (error.status === 401) {
        authService.logout().subscribe({
          complete: () => {
            // Optional: Add a query parameter to show a message on the login page
            router.navigate(['/login'], { queryParams: { sessionExpired: true } });
          }
        });
        // We still throw an error to stop the current observable chain,
        // but it's a more specific message.
        return throwError(() => new Error('Your session has expired. Please log in again.'));
      }

      // --- Step 2: Check for a structured VALIDATION error ---
      // If it's a validation error, we pass the full error body through.
      // The component will be responsible for handling it.
      if (isApiErrorDetailedValidation(error.error)) {
        return throwError(() => error.error); // <-- Pass the structured object
      }

      // --- Step 3: For all OTHER errors, parse a simple message ---
      const errorMessage = getErrorMessage(error);

      // --- Step 3: (Optional) Log for developers ---
      console.error({
        message: 'HTTP Error Intercepted',
        status: error.status,
        error, // The original HttpErrorResponse object
        parsedMessage: errorMessage, // The clean message we extracted
      });

      // --- Step 4: Re-throw the clean, user-friendly error message ---
      // This is what the component's catchError block will now receive.
      return throwError(() => new Error(errorMessage));
    })
  );
};
