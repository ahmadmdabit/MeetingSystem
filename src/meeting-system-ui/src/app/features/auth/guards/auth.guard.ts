import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/api/auth.service';

export const authGuard = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  
  if (authService.getToken()) {
    return true;
  }
  
  // Redirect to login if not authenticated
  return router.createUrlTree(['/login']);
};

export const adminGuard = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  
  // Check if user is authenticated first
  if (!authService.getToken()) {
    return router.createUrlTree(['/login']);
  }
  
  // In a real application, you would check user roles here
  // For now, we'll just allow access
  return true;
};