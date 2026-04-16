import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { DeploymentsService, DeploymentDto } from './deployments.service';
import { ServicesService, ServiceDto } from '../services/services.service';

@Component({
  selector: 'limen-deployments',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, HlmButton, HlmCard, HlmCardContent, HlmCardHeader, HlmCardTitle],
  templateUrl: './deployments.html',
})
export class Deployments implements OnInit {
  private deploymentsService = inject(DeploymentsService);
  private servicesService = inject(ServicesService);
  private route = inject(ActivatedRoute);

  deployments = signal<DeploymentDto[]>([]);
  services = signal<ServiceDto[]>([]);
  selectedServiceId = signal<string>('');

  ngOnInit() {
    this.servicesService.list().subscribe(s => this.services.set(s));
    const qp = this.route.snapshot.queryParamMap.get('serviceId');
    if (qp) {
      this.selectedServiceId.set(qp);
    }
    this.refresh();
  }

  refresh() {
    const sid = this.selectedServiceId();
    this.deploymentsService.list(sid || undefined).subscribe(d => this.deployments.set(d));
  }

  onServiceChange() {
    this.refresh();
  }

  shortDigest(digest: string): string {
    return digest.startsWith('sha256:') ? digest.substring(7, 19) : digest.substring(0, 12);
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Succeeded': return 'text-green-700';
      case 'Failed':
      case 'RolledBack': return 'text-red-700';
      case 'Queued':
      case 'InProgress': return 'text-amber-700';
      case 'Cancelled': return 'text-muted-foreground';
      default: return '';
    }
  }

  isTerminal(status: string): boolean {
    return ['Succeeded', 'Failed', 'RolledBack', 'Cancelled'].includes(status);
  }

  cancel(id: string) {
    this.deploymentsService.cancel(id).subscribe(() => this.refresh());
  }

  serviceName(serviceId: string): string {
    const s = this.services().find(x => x.id === serviceId);
    return s ? s.name : serviceId;
  }
}
