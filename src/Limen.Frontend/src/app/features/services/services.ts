import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { ServicesService, ServiceDto, CreateServiceDto } from './services.service';
import { DeploymentsService } from '../deployments/deployments.service';

@Component({
  selector: 'limen-services',
  standalone: true,
  imports: [FormsModule, RouterLink, HlmButton, HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle],
  templateUrl: './services.html',
})
export class Services implements OnInit {
  private svc = inject(ServicesService);
  private deploymentsService = inject(DeploymentsService);
  private router = inject(Router);

  services = signal<ServiceDto[]>([]);
  showForm = signal(false);
  deployFormServiceId = signal<string | null>(null);

  deployForm: { imageTag: string; imageDigest: string } = { imageTag: '', imageDigest: '' };

  form: CreateServiceDto = {
    name: '',
    targetNodeId: '',
    containerName: '',
    internalPort: 80,
    image: '',
    autoDeploy: false,
  };

  ngOnInit() {
    this.refresh();
  }

  refresh() {
    this.svc.list().subscribe(x => this.services.set(x));
  }

  submit() {
    this.svc.create(this.form).subscribe(() => {
      this.showForm.set(false);
      this.form = { name: '', targetNodeId: '', containerName: '', internalPort: 80, image: '', autoDeploy: false };
      this.refresh();
    });
  }

  openDeployForm(s: ServiceDto) {
    this.deployFormServiceId.set(s.id);
    const colonIdx = s.image.indexOf(':');
    this.deployForm = {
      imageTag: colonIdx >= 0 ? s.image.substring(colonIdx + 1) : '',
      imageDigest: '',
    };
  }

  closeDeployForm() {
    this.deployFormServiceId.set(null);
    this.deployForm = { imageTag: '', imageDigest: '' };
  }

  submitDeploy(serviceId: string) {
    this.deploymentsService.create({
      serviceId,
      imageDigest: this.deployForm.imageDigest,
      imageTag: this.deployForm.imageTag,
    }).subscribe(() => {
      this.closeDeployForm();
      this.router.navigate(['/deployments'], { queryParams: { serviceId } });
    });
  }
}
