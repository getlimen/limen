import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { ServicesService, ServiceDto, CreateServiceDto } from './services.service';

@Component({
  selector: 'limen-services',
  standalone: true,
  imports: [FormsModule, RouterLink, HlmButton, HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle],
  templateUrl: './services.html',
})
export class Services implements OnInit {
  private svc = inject(ServicesService);
  services = signal<ServiceDto[]>([]);
  showForm = signal(false);

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
}
