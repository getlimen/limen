import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { RoutesService, RouteDto, AddRouteDto } from './routes.service';

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
}
