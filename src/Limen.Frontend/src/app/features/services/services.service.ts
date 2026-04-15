import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface ServiceDto {
  id: string;
  name: string;
  targetNodeId: string;
  containerName: string;
  internalPort: number;
  image: string;
  autoDeploy: boolean;
}

export interface CreateServiceDto {
  name: string;
  targetNodeId: string;
  containerName: string;
  internalPort: number;
  image: string;
  autoDeploy: boolean;
}

@Injectable({ providedIn: 'root' })
export class ServicesService {
  constructor(private http: HttpClient) {}
  list() { return this.http.get<ServiceDto[]>('/api/services/'); }
  create(dto: CreateServiceDto) { return this.http.post<{ id: string }>('/api/services/', dto); }
}
