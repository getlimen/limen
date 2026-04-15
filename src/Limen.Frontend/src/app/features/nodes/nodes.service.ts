import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface NodeDto { id: string; name: string; roles: string[]; status: string; lastSeenAt?: string; }
export interface ProvisioningKeyResult { id: string; plaintextKey: string; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class NodesService {
  constructor(private http: HttpClient) {}
  list() { return this.http.get<NodeDto[]>('/api/nodes'); }
  createKey(roles: string[]) {
    return this.http.post<ProvisioningKeyResult>('/api/nodes/provisioning-keys', { roles });
  }
}
