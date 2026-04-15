import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'limen-login',
  standalone: true,
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-50">
      <div class="p-8 bg-white rounded-lg shadow-md max-w-sm w-full text-center">
        <h1 class="text-2xl font-bold mb-4">Limen</h1>
        <p class="text-slate-600 mb-6">Sign in to manage your infrastructure.</p>
        <button (click)="auth.login()" class="w-full px-4 py-2 bg-slate-900 text-white rounded hover:bg-slate-800">
          Sign in with OIDC
        </button>
      </div>
    </div>`,
})
export class LoginComponent { auth = inject(AuthService); }
