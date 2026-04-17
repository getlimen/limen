import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface PublicRoutePolicy { routeId: string; mode: string; hostname: string; }
export interface LoginPasswordRequest { routeId: string; email: string; password: string; returnTo?: string; }
export interface LoginPasswordResponse { ok: boolean; expiresAt: string; redirect: string; }
export interface MagicRequestRequest { routeId: string; email: string; }

@Injectable({ providedIn: 'root' })
export class ResourceAuthService {
  constructor(private http: HttpClient) {}

  getPublicPolicy(routeId: string) {
    return this.http.get<PublicRoutePolicy>(`/api/public/route-policy/${routeId}`);
  }

  loginWithPassword(req: LoginPasswordRequest) {
    return this.http.post<LoginPasswordResponse>('/auth/login-password', req);
  }

  requestMagicLink(req: MagicRequestRequest) {
    return this.http.post<{ ok: boolean }>('/auth/magic-request', req);
  }
}
