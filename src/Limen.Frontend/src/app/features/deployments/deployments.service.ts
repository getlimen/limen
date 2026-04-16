import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';

export interface DeploymentDto {
  id: string;
  serviceId: string;
  targetNodeId: string;
  imageDigest: string;
  imageTag: string;
  status: string;
  currentStage: string;
  queuedAt: string;
  startedAt?: string | null;
  endedAt?: string | null;
}

export interface CreateDeploymentDto {
  serviceId: string;
  imageDigest: string;
  imageTag: string;
}

@Injectable({ providedIn: 'root' })
export class DeploymentsService {
  constructor(private http: HttpClient) {}

  list(serviceId?: string) {
    let params = new HttpParams();
    if (serviceId) params = params.set('serviceId', serviceId);
    return this.http.get<DeploymentDto[]>('/api/deployments', { params });
  }

  logs(id: string) {
    return this.http.get(`/api/deployments/${id}/logs`, { responseType: 'text' });
  }

  create(dto: CreateDeploymentDto) {
    return this.http.post<{ id: string }>('/api/deployments', dto);
  }

  cancel(id: string) {
    return this.http.post(`/api/deployments/${id}/cancel`, null);
  }
}
