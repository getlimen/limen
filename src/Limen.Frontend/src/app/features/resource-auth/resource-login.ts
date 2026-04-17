import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { ResourceAuthService, PublicRoutePolicy } from './resource-auth.service';

@Component({
  selector: 'limen-resource-login',
  standalone: true,
  imports: [FormsModule, HlmButton, HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle],
  templateUrl: './resource-login.html',
})
export class ResourceLogin implements OnInit {
  private route = inject(ActivatedRoute);
  private svc = inject(ResourceAuthService);

  routeId = '';
  returnTo = '';

  policy = signal<PublicRoutePolicy | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  magicSent = signal(false);

  email = '';
  password = '';

  ngOnInit() {
    this.routeId = this.route.snapshot.queryParamMap.get('routeId') ?? '';
    this.returnTo = this.route.snapshot.queryParamMap.get('returnTo') ?? '/';

    if (!this.routeId) {
      this.error.set('Missing routeId parameter.');
      this.loading.set(false);
      return;
    }

    this.svc.getPublicPolicy(this.routeId).subscribe({
      next: p => {
        this.policy.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load authentication policy. The route may not exist.');
        this.loading.set(false);
      },
    });
  }

  submitPassword() {
    this.error.set(null);
    this.svc.loginWithPassword({ routeId: this.routeId, email: this.email, password: this.password, returnTo: this.returnTo }).subscribe({
      next: res => {
        window.location.href = res.redirect;
      },
      error: err => {
        if (err.status === 401) {
          this.error.set('Invalid email or password.');
        } else {
          this.error.set('Sign-in failed. Please try again.');
        }
      },
    });
  }

  submitMagic() {
    this.error.set(null);
    this.svc.requestMagicLink({ routeId: this.routeId, email: this.email }).subscribe({
      next: () => {
        this.magicSent.set(true);
      },
      error: () => {
        // Show generic message to avoid email enumeration
        this.magicSent.set(true);
      },
    });
  }

  loginSso() {
    window.location.href = `/auth/resource-oidc?routeId=${this.routeId}&returnTo=${encodeURIComponent(this.returnTo)}`;
  }
}
