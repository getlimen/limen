import { Component, inject } from '@angular/core';
import { HlmButton } from '@spartan-ng/helm/button';
import {
  HlmCard,
  HlmCardContent,
  HlmCardDescription,
  HlmCardHeader,
  HlmCardTitle,
} from '@spartan-ng/helm/card';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'limen-login',
  standalone: true,
  imports: [HlmButton, HlmCard, HlmCardHeader, HlmCardTitle, HlmCardDescription, HlmCardContent],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-background p-4">
      <section hlmCard class="w-full max-w-sm">
        <header hlmCardHeader>
          <h1 hlmCardTitle class="text-2xl">Limen</h1>
          <p hlmCardDescription>Sign in to manage your infrastructure.</p>
        </header>
        <div hlmCardContent>
          <button hlmBtn class="w-full" (click)="auth.login()">
            Sign in with OIDC
          </button>
        </div>
      </section>
    </div>`,
})
export class LoginComponent {
  auth = inject(AuthService);
}
