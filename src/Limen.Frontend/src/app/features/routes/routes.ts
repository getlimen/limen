import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { RoutesService, RouteDto, AddRouteDto, AuthPolicyDto, SetAuthPolicyDto } from './routes.service';

@Component({
  selector: 'limen-routes',
  standalone: true,
  imports: [FormsModule, RouterLink, HlmButton, HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle],
  templateUrl: './routes.html',
})
export class Routes implements OnInit {
  private svc = inject(RoutesService);
  routes = signal<RouteDto[]>([]);
  showForm = signal(false);
  expandedRouteId = signal<string | null>(null);
  policyMap = signal<Record<string, AuthPolicyDto>>({});
  policyFormMap = signal<Record<string, SetAuthPolicyDto & { allowedEmailsText: string }>>({});

  form: AddRouteDto = {
    serviceId: '',
    proxyNodeId: '',
    hostname: '',
    tlsEnabled: true,
    authPolicy: 'none',
  };

  ngOnInit() {
    this.refresh();
  }

  refresh() {
    this.svc.list().subscribe(x => this.routes.set(x));
  }

  submit() {
    this.svc.create(this.form).subscribe(() => {
      this.showForm.set(false);
      this.form = { serviceId: '', proxyNodeId: '', hostname: '', tlsEnabled: true, authPolicy: 'none' };
      this.refresh();
    });
  }

  togglePolicyEditor(routeId: string) {
    if (this.expandedRouteId() === routeId) {
      this.expandedRouteId.set(null);
      return;
    }
    this.expandedRouteId.set(routeId);
    this.svc.getAuthPolicy(routeId).subscribe(policy => {
      this.policyMap.update(m => ({ ...m, [routeId]: policy }));
      this.policyFormMap.update(m => ({
        ...m,
        [routeId]: {
          mode: policy.mode,
          password: '',
          cookieScope: policy.cookieScope,
          allowedEmails: policy.allowedEmails,
          allowedEmailsText: policy.allowedEmails.join('\n'),
        },
      }));
    });
  }

  cancelPolicyEditor() {
    this.expandedRouteId.set(null);
  }

  savePolicyEditor(routeId: string) {
    const form = this.policyFormMap()[routeId];
    if (!form) { return; }
    const dto: SetAuthPolicyDto = {
      mode: form.mode,
      cookieScope: form.cookieScope,
      allowedEmails: form.mode === 'allowlist'
        ? form.allowedEmailsText.split(/[\n,]/).map(e => e.trim()).filter(e => e.length > 0)
        : undefined,
      password: form.mode === 'password' && form.password ? form.password : undefined,
    };
    this.svc.setAuthPolicy(routeId, dto).subscribe(() => {
      this.expandedRouteId.set(null);
      this.refresh();
    });
  }

  getPolicyForm(routeId: string): (SetAuthPolicyDto & { allowedEmailsText: string }) | null {
    return this.policyFormMap()[routeId] ?? null;
  }

  updatePolicyFormField(routeId: string, field: string, value: string) {
    this.policyFormMap.update(m => ({
      ...m,
      [routeId]: { ...m[routeId], [field]: value },
    }));
  }
}
