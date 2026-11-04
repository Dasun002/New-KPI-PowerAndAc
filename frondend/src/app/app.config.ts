/* File: app.config.ts
   Description: Angular application configuration
   Purpose: Centralized configuration for providers, routing, HTTP client setup,
   animations, MSAL authentication, and JWT interceptor registration.
*/

import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';
import { MsalService, MsalGuard, MsalInterceptor, MsalBroadcastService, MSAL_INSTANCE, MSAL_GUARD_CONFIG, MSAL_INTERCEPTOR_CONFIG } from '@azure/msal-angular';
import { InteractionType } from '@azure/msal-browser';
import { createMsalInstance, loginRequest } from './auth-config';
import { JwtInterceptor } from './interceptors/jwt.interceptor';

import { routes } from './app.routes';

/* ========== APPLICATION CONFIGURATION ========== */

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: false }),
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimations(),
    provideRouter(routes),
    provideToastr(),
    { provide: MSAL_INSTANCE, useFactory: createMsalInstance },
    MsalService,
    MsalBroadcastService,
    MsalGuard,
    {
      provide: MSAL_GUARD_CONFIG,
      useValue: {
        interactionType: InteractionType.Popup,
        authRequest: loginRequest
      }
    },
    {
      provide: MSAL_INTERCEPTOR_CONFIG, useValue: {
        interactionType: InteractionType.Popup,
        protectedResourceMap: new Map([
          // ['https://localhost:7251', ['api://eaea79e0-86c3-46ae-8c51-9d7c1c997ee2/access_as_user']],
          // ['http://localhost:5043', ['api://eaea79e0-86c3-46ae-8c51-9d7c1c997ee2/access_as_user']]
        ])
      }
    },
    { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true } // Disable MSAL Interceptor for now
  ]
};