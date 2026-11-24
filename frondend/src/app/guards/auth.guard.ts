/* File: auth.guard.ts
   Description: Route authentication and authorization guard
   Purpose: Protects routes by verifying user authentication and checking
   role-based access control before allowing navigation.
*/

import { Injectable, inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/* ========== PERMISSIONS SERVICE ========== */

/* Service that checks route permissions */
@Injectable({
    providedIn: 'root'
})
class PermissionsService {
    constructor(private authService: AuthService, private router: Router) { }

    /* Validates if user can activate a route based on authentication and role */
    canActivate(next: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
        const user = this.authService.userValue;
        if (user) {
            /* Check if route has role restrictions */
            const allowedRoles = next.data['roles'] as Array<string>;
            if (allowedRoles && allowedRoles.length > 0) {
                /* Verify user has required role */
                if (!allowedRoles.includes(user.role)) {
                    /* Role not authorized - redirect to home */
                    this.router.navigate(['/']);
                    return false;
                }
            }
            /* User is authenticated and authorized */
            return true;
        }

        /* User not logged in - redirect to login with return URL */
        this.router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
        return false;
    }
}

/* ========== AUTH GUARD FUNCTION ========== */

/* Functional guard that checks route permissions using dependency injection */
export const AuthGuard: CanActivateFn = (next: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean => {
    return inject(PermissionsService).canActivate(next, state);
}
