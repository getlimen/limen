import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AdminInfo { subject: string; email: string; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _admin = signal<AdminInfo | null>(null);
  readonly admin = this._admin.asReadonly();

  constructor(private http: HttpClient) {}

  async refresh(): Promise<AdminInfo | null> {
    try {
      const me = await firstValueFrom(this.http.get<AdminInfo>('/auth/me'));
      this._admin.set(me);
      return me;
    } catch {
      this._admin.set(null);
      return null;
    }
  }

  login() { window.location.href = '/auth/login'; }

  async signOut() {
    await firstValueFrom(this.http.post('/auth/signout', {}));
    this._admin.set(null);
  }
}
