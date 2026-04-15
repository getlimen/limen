import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSeparator } from '@spartan-ng/helm/separator';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'limen-dashboard',
  standalone: true,
  imports: [RouterLink, HlmButton, HlmSeparator],
  template: `
    <div class="p-8">
      <header class="flex justify-between items-center mb-4">
        <h1 class="text-3xl font-bold">Limen</h1>
        <div class="flex items-center gap-4">
          <a routerLink="/nodes" class="text-sm underline">Nodes</a>
          <span class="text-sm text-muted-foreground">{{ auth.admin()?.email }}</span>
          <button hlmBtn variant="outline" size="sm" (click)="auth.signOut()">Sign out</button>
        </div>
      </header>
      <div hlmSeparator class="mb-8"></div>
      <section class="text-muted-foreground">
        No nodes enrolled yet.
      </section>
    </div>`,
})
export class DashboardComponent {
  auth = inject(AuthService);
}
