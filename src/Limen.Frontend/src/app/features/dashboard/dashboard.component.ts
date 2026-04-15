import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'limen-dashboard',
  standalone: true,
  template: `
    <div class="p-8">
      <header class="flex justify-between items-center mb-8">
        <h1 class="text-3xl font-bold">Limen</h1>
        <div class="flex items-center gap-4">
          <span class="text-sm text-slate-600">{{ auth.admin()?.email }}</span>
          <button (click)="auth.signOut()" class="px-3 py-1 text-sm border rounded">Sign out</button>
        </div>
      </header>
      <section class="text-slate-500">
        No nodes enrolled yet.
      </section>
    </div>`,
})
export class DashboardComponent { auth = inject(AuthService); }
