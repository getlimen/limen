import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  { path: 'resource-login', loadComponent: () => import('./features/resource-auth/resource-login').then(m => m.ResourceLogin) },
  { path: '', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent), canActivate: [authGuard] },
  { path: 'nodes', loadComponent: () => import('./features/nodes/nodes').then(m => m.NodesComponent), canActivate: [authGuard] },
  { path: 'services', loadComponent: () => import('./features/services/services').then(m => m.Services), canActivate: [authGuard] },
  { path: 'routes', loadComponent: () => import('./features/routes/routes').then(m => m.Routes), canActivate: [authGuard] },
  { path: 'deployments', loadComponent: () => import('./features/deployments/deployments').then(m => m.Deployments), canActivate: [authGuard] },
  { path: 'deployments/:id', loadComponent: () => import('./features/deployments/deployment-detail').then(m => m.DeploymentDetail), canActivate: [authGuard] },
];
