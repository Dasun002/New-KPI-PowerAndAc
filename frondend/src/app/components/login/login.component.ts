import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { first } from 'rxjs/operators';
import { loginRequest } from '../../auth-config';
import { AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
    serviceId = '';
    loading = false;
    error = '';
    isAzureAuthenticated = false;
    azureEmail = '';
    isDevMode = !environment.production;

    constructor(
        private authService: AuthService,
        private router: Router,
        private route: ActivatedRoute,
        private msalService: MsalService,
        private msalBroadcastService: MsalBroadcastService
    ) { }

    ngOnInit(): void {
        console.log('[Login] Checking for existing Azure session and redirect results...');

        this.msalService.instance.handleRedirectPromise().then(result => {
            if (result) {
                if (result.account) {
                    console.log('[Login] Redirect success:', result.account.username);
                    this.setAzureState(result.account.username);
                }
                this.msalService.instance.setActiveAccount(result.account);
                return;
            }

            const activeAccount = this.msalService.instance.getActiveAccount();
            const allAccounts = this.msalService.instance.getAllAccounts();

            if (activeAccount) {
                console.log('[Login] Active account found:', activeAccount.username);
                this.setAzureState(activeAccount.username);
            } else if (allAccounts.length > 0) {
                console.log('[Login] No active account, setting first available:', allAccounts[0].username);
                this.msalService.instance.setActiveAccount(allAccounts[0]);
                this.setAzureState(allAccounts[0].username);
            }
        }).catch(err => {
            console.error('[Login] MSAL handleRedirectPromise error:', err);
            this.error = 'Azure login failed to process redirect';
        });

        if (this.authService.userValue) {
            this.router.navigate(['/dashboard']);
        }
    }

    private setAzureState(email: string) {
        this.isAzureAuthenticated = true;
        this.azureEmail = email;
    }

    login() {
        this.loading = true;
        this.error = '';

        if (!this.isAzureAuthenticated || !this.azureEmail) {
            this.error = 'Please sign in with Microsoft first';
            this.loading = false;
            return;
        }

        this.authService.verifyAzureLogin(this.azureEmail, this.serviceId)
            .pipe(first())
            .subscribe({
                next: () => {
                    this.handleLoginSuccess();
                },
                error: (error) => {
                    console.error('Authentication Error:', error);
                    this.error = this.getLoginErrorMessage(error);
                    this.loading = false;
                }
            });
    }

    private getLoginErrorMessage(error: any): string {
        if (error?.status === 0 || error?.error instanceof ProgressEvent) {
            return 'Cannot reach the backend. Please check that the API is running and the browser can access it.';
        }

        const backendMessage = error?.error;
        if (typeof backendMessage === 'string' && backendMessage.trim()) {
            return backendMessage;
        }

        if (backendMessage?.message) {
            return backendMessage.message;
        }

        return 'Authentication failed. Please verify your Service ID is registered in the system.';
    }

    signInWithAzure() {
        this.loading = true;
        this.error = '';
        console.log('[Login] Initiating loginRedirect...');
        this.msalService.loginRedirect(loginRequest);
    }

    devSignIn() {
        this.setAzureState('mathukandagesanduni22@gmail.com');
    }

    cancelAzure() {
        this.isAzureAuthenticated = false;
        this.azureEmail = '';
        this.serviceId = '';
        this.error = '';
        console.log('[Login] Logging out from Azure...');
        this.msalService.logoutRedirect();
    }

    private handleLoginSuccess() {
        const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
        this.router.navigate([returnUrl]);
    }
}