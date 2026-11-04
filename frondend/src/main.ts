/* File: main.ts
   Description: Application bootstrap file
   Purpose: Entry point for the Angular application. Initializes Zone.js,
   sets up MSAL authentication, handles Azure redirect responses, and bootstraps
   the root App component.
*/

import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { createMsalInstance } from './app/auth-config';

/* ========== APPLICATION INITIALIZATION ========== */

const msalInstance = createMsalInstance();

/* Asynchronous initialization sequence */
(async () => {
  try {
    await msalInstance.initialize();

    /* Process Azure redirect responses before Angular bootstrap */
    const response = await msalInstance.handleRedirectPromise();

    /* Set active account from redirect response or cached accounts */
    if (response && response.account) {
      msalInstance.setActiveAccount(response.account);
      /* Clean up URL to remove auth codes */
      const cleanUrl = window.location.origin + window.location.pathname;
      window.history.replaceState({}, document.title, cleanUrl);
    } else {
      /* Use first cached account if no redirect response */
      const accounts = msalInstance.getAllAccounts();
      if (accounts.length > 0 && !msalInstance.getActiveAccount()) {
        msalInstance.setActiveAccount(accounts[0]);
      }
    }
  } catch (e) {
    console.warn('MSAL initialization or handleRedirectPromise error', e);
    // Store error for debugging
    sessionStorage.setItem('msalError', JSON.stringify(e));
  } finally {
    bootstrapApplication(App, appConfig).catch((err) => console.error(err));
  }
})();