/* File: auth-config.ts
   Description: Azure MSAL authentication configuration
   Purpose: Configures Azure AD B2C authentication including client ID,
   authority, redirect URI, and login request scopes.
*/

import { Configuration, PublicClientApplication } from '@azure/msal-browser';

/* ========== MSAL CONFIGURATION ========== */
export const msalConfig: Configuration = {
  auth: {
    clientId: '882b0135-bd0b-4ce6-8bef-c4f66ad3d0cc',
    authority: 'https://login.microsoftonline.com/534253fc-dfb6-462f-b5ca-cbe81939f5ee',
    //redirectUri: window.location.origin
	redirectUri: window.location.origin + '/KPIMonitoring/'
  },
  cache: {
    cacheLocation: 'localStorage',
    // storeAuthStateInCookie: true  // optional
  }
};

/* MSAL login request configuration */
export const loginRequest = {
  scopes: ['openid', 'profile', 'offline_access']
};

/* ========== MSAL INSTANCE MANAGEMENT ========== */
// Singleton MSAL instance
let msalInstance: PublicClientApplication;

/**
 * Factory function to create (or return) the MSAL instance
 * Angular DI requires a concrete instance, not null
 */
export function createMsalInstance(): PublicClientApplication {
  if (!msalInstance) {
    msalInstance = new PublicClientApplication(msalConfig);
  }
  return msalInstance;
}