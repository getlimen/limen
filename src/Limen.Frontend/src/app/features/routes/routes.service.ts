import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface RouteDto {
  id: string;
  serviceId: string;
  proxyNodeId: string;
  hostname: string;
  tlsEnabled: boolean;
  authPolicy: string;
}

export interface AddRouteDto {
  serviceId: string;
  proxyNodeId: string;
  hostname: string;
  tlsEnabled: boolean;
  authPolicy: string;
}

export interface AuthPolicyDto {
  routeId: string;
  mode: string;
  cookieScope: string;
  allowedEmails: string[];
}

export interface SetAuthPolicyDto {
  mode: string;
  password?: string;
  cookieScope: string;
  allowedEmails?: string[];
}

@Injectable({ providedIn: 'root' })
export class RoutesService {
  constructor(private http: HttpClient) {}
  list() { return this.http.get<RouteDto[]>('/api/routes/'); }
  create(dto: AddRouteDto) { return this.http.post<{ id: string }>('/api/routes/', dto); }
  getAuthPolicy(routeId: string) { return this.http.get<AuthPolicyDto>(`/api/routes/${routeId}/auth-policy`); }
  setAuthPolicy(routeId: string, dto: SetAuthPolicyDto) { return this.http.post(`/api/routes/${routeId}/auth-policy`, dto); }
}
