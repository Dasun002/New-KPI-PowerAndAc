/* File: app.routes.ts
   Description: Application routing configuration
   Purpose: Defines all application routes with their components and guard protections.
   Routes: Login, Dashboard, Overall KPI, Platform KPI, Admin pages
*/

import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { Q1Component } from './components/pages/2026-q1/2026-q1.component';
import { AuthGuard } from './guards/auth.guard';

/* ========== ROUTE COMPONENT IMPORTS ========== */

/* Overall KPI Components */
import { CurrentMonthComponent } from './components/pages/overall/current-month/current-month.component';
// Placeholder for future previous month component
/* Platform KPI Components */
import { BbAnwComponent } from './components/pages/platform/bb-anw/bb-anw.component';
import { EnterpriseKpiComponent } from './components/pages/platform/enterprise-kpi/enterprise-kpi.component';
import { IpNwOpComponent } from './components/pages/platform/ip-nw-op/ip-nw-op.component';
import { OtherKpiComponent } from './components/pages/platform/other-kpi/other-kpi.component';
import { OtnOpComponent } from './components/pages/platform/otn-op/otn-op.component';
import { RoutineMtncComponent } from './components/pages/platform/routine-mtnc/routine-mtnc.component';
import { ServiceFulfilmentComponent } from './components/pages/platform/service-fulfilment/service-fulfilment.component';
import { TmActivityPlanComponent } from './components/pages/platform/tm-activity-plan/tm-activity-plan.component';
import { OtherOperatorComponent } from './components/pages/platform/other-operator/other-operator.component';
/* Admin Components */
import { MsalGuard } from '@azure/msal-angular';
import { AdminRegistrationComponent } from './components/pages/admin/admin-registration/admin-registration.component';
import { BbAnwComponent as AdminBbAnwComponent } from './components/pages/admin/bb-anw/bb-anw.component';
import { EmailServiceComponent } from './components/pages/admin/email-service/email-service.component';
import { EnterpriseKpiComponent as AdminEnterpriseKpiComponent } from './components/pages/admin/enterprise-kpi/enterprise-kpi.component';
import { FinalTableComponent } from './components/pages/admin/final-table/final-table.component';
import { AdminIpNwOpComponent } from './components/pages/admin/ip-nw-op/ip-nw-op.component';
import { OtherKpiComponent as AdminOtherKpiComponent } from './components/pages/admin/other-kpi/other-kpi.component';
import { OtnOp1Component } from './components/pages/admin/otn-op-1/otn-op-1.component';
import { OtnOp2Component } from './components/pages/admin/otn-op-2/otn-op-2.component';
import { RegionManagementComponent } from './components/pages/admin/region-management/region-management.component';
import { AdminRoutineMtncComponent } from './components/pages/admin/routine-mtnc/routine-mtnc.component';
import { AdminServiceFulfilmentComponent } from './components/pages/admin/service-fulfilment/service-fulfilment.component';
import { TowerMtncComponent } from './components/pages/admin/tower-mtnc/tower-mtnc.component';

import { OtherOperatorComponent as AdminOtherOperatorComponent } from './components/pages/admin/other-operator/other-operator.component';

import { UserRegistrationComponent } from './components/pages/admin/user-registration/user-registration.component';

/* ========== ROUTE DEFINITIONS ========== */

export const routes: Routes = [
  /* Default route redirects to login */
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  /* Authentication page */
  { path: 'login', loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) },
  /* Main dashboard with KPI overview */
  { path: 'dashboard', component: DashboardComponent, canActivate: [AuthGuard] },
  /* Q1 Route */
  { path: 'q1', component: Q1Component, canActivate: [AuthGuard] },
  /* Overall KPI Routes */
  { path: 'overall/current-month', component: CurrentMonthComponent, canActivate: [AuthGuard] },
  // Placeholder for future previous month route
  /* Analytics Route */
  { path: 'analytics', loadComponent: () => import('./components/pages/analytics/analytics.component').then(m => m.AnalyticsComponent), canActivate: [AuthGuard] },
  /* Platform KPI Routes */
  { path: 'platform/service-fulfilment', component: ServiceFulfilmentComponent, canActivate: [AuthGuard] },
  { path: 'platform/enterprise-kpi', component: EnterpriseKpiComponent, canActivate: [AuthGuard] },
  { path: 'platform/other-kpi', component: OtherKpiComponent, canActivate: [AuthGuard] },
  { path: 'platform/ip-nw-op', component: IpNwOpComponent, canActivate: [AuthGuard] },
  { path: 'platform/bb-anw', component: BbAnwComponent, canActivate: [AuthGuard] },
  { path: 'platform/otn-op', component: OtnOpComponent, canActivate: [AuthGuard] },
  { path: 'platform/tm-activity-plan', component: TmActivityPlanComponent, canActivate: [AuthGuard] },
  { path: 'platform/routine-mtnc', component: RoutineMtncComponent, canActivate: [AuthGuard] },
  { path: 'platform/tower-mtce-achievement', component: OtherOperatorComponent, canActivate: [AuthGuard] },
  
  /* Admin Routes - requires Admin or SuperAdmin role */
  { path: 'admin/admin-registration', component: AdminRegistrationComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/user-registration', component: UserRegistrationComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/service-fulfilment', component: AdminServiceFulfilmentComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/region-management', component: RegionManagementComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/ip-nw-op', component: AdminIpNwOpComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/bb-anw', component: AdminBbAnwComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/otn-op-1', component: OtnOp1Component, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/otn-op-2', component: OtnOp2Component, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/tower-mtce-achievement', component: AdminOtherOperatorComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },

  { path: 'admin/tm-activity-plan', component: TowerMtncComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },

  { path: 'admin/routine-mtnc', component: AdminRoutineMtncComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/email-service', component: EmailServiceComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/final-table', component: FinalTableComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin', 'PlatformAdmin', 'User'] } },
  { path: 'admin/enterprise-kpi', component: AdminEnterpriseKpiComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: 'admin/other-kpi', component: AdminOtherKpiComponent, canActivate: [AuthGuard], data: { roles: ['Admin', 'SuperAdmin'] } },
  { path: '**', redirectTo: 'dashboard' },
  { path: 'dashboard', canActivate: [MsalGuard], loadComponent: () => import('./components/dashboard/dashboard.component').then(m => m.DashboardComponent)}
];
