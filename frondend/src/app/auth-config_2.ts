/* File: auth-config.ts
   Description: Azure MSAL authentication configuration
   Purpose: Configures Azure AD B2C authentication including client ID,
   authority, redirect URI, and login request scopes.
*/

import { InteractionType, PublicClientApplication } from '@azure/msal-browser';

/* ========== MSAL CONFIGURATION ========== */

export const msalConfig = {
    auth: {
        clientId: '2fd0bd33-873a-4744-8762-e8ae70c8b395',
        authority: 'https://login.microsoftonline.com/534253fc-dfb6-462f-b5ca-cbe81939f5ee',
        redirectUri: window.location.origin
    },
    cache: {
        cacheLocation: 'localStorage',
        storeAuthStateInCookie: true
    }
};

/* MSAL login request configuration */
export const loginRequest = {
    /* Request openid, profile, and offline_access scopes */
    /* Note: API scope temporarily disabled - causes AADSTS500011 (resource not found) */
    scopes: ['openid', 'profile', 'offline_access']
};

/* ========== MSAL INSTANCE MANAGEMENT ========== */

/* Singleton MSAL instance */
let msalInstance: PublicClientApplication | null = null;
/* Flag to track if MSAL has been initialized */
let msalInitialized = false;

export function createMsalInstance(): PublicClientApplication {
    if (!msalInstance) {
        msalInstance = new PublicClientApplication(msalConfig as any);
    }
    // If already initialized in main.ts, return the same instance
    return msalInstance;
}

export function markMsalInitialized(): void {
    msalInitialized = true;
}

export function isMsalInitialized(): boolean {
    return msalInitialized;
}

export function getMsalInstance(): PublicClientApplication | null {
    return msalInstance;
}
