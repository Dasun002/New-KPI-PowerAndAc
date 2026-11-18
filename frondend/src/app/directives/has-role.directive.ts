/* File: has-role.directive.ts
   Description: Role-based element visibility directive
   Purpose: Conditionally renders template based on user role.
   Usage: *appHasRole="['Admin', 'SuperAdmin']"
*/

import { Directive, Input, TemplateRef, ViewContainerRef, OnInit } from '@angular/core';
import { AuthService } from '../services/auth.service';

/* ========== HAS-ROLE DIRECTIVE ========== */

@Directive({
    selector: '[appHasRole]',
    standalone: true
})
export class HasRoleDirective implements OnInit {
    /* Array of allowed roles for element visibility */
    @Input() appHasRole: string[] = [];

    constructor(
        private templateRef: TemplateRef<any>,
        private viewContainer: ViewContainerRef,
        private authService: AuthService
    ) { }

    /* Check user role and show/hide element accordingly */
    ngOnInit() {
        /* Get current user's role */
        const userRole = this.authService.getRole();

        /* No roles defined - show by default */
        if (!this.appHasRole || this.appHasRole.length === 0) {
            this.viewContainer.createEmbeddedView(this.templateRef);
            return;
        }

        /* Show element if user role matches allowed roles */
        if (userRole && this.appHasRole.includes(userRole)) {
            this.viewContainer.createEmbeddedView(this.templateRef);
        } else {
            /* Hide element if role not allowed */
            this.viewContainer.clear();
        }
    }
}
