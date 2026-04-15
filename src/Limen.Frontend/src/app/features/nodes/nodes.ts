import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle } from '@spartan-ng/helm/card';
import { NodesService, NodeDto, ProvisioningKeyResult } from './nodes.service';

@Component({
  selector: 'limen-nodes',
  standalone: true,
  imports: [DatePipe, HlmButton, HlmCard, HlmCardContent, HlmCardDescription, HlmCardHeader, HlmCardTitle],
  template: `
    <div class="p-8 min-h-screen bg-muted">
      <header class="flex justify-between items-center mb-6">
        <h1 class="text-3xl font-bold">Nodes</h1>
        <button hlmBtn (click)="createKey()">Add node</button>
      </header>

      @if (key(); as k) {
        <section hlmCard class="mb-6">
          <header hlmCardHeader>
            <h2 hlmCardTitle class="text-lg">Run this on the new host</h2>
            <p hlmCardDescription>The provisioning key expires at {{ k.expiresAt | date:'medium' }}.</p>
          </header>
          <div hlmCardContent>
            <pre class="bg-background p-4 rounded text-xs overflow-auto"><code>LIMEN_PROVISIONING_KEY={{ k.plaintextKey }}
LIMEN_ROLES=docker
docker compose up -d</code></pre>
          </div>
        </section>
      }

      <section hlmCard>
        <header hlmCardHeader>
          <h2 hlmCardTitle class="text-lg">Enrolled nodes</h2>
          <p hlmCardDescription>Polls every 5 seconds.</p>
        </header>
        <div hlmCardContent>
          @if (nodes().length === 0) {
            <p class="text-muted-foreground">No nodes enrolled yet.</p>
          } @else {
            <table class="w-full text-left">
              <thead class="text-xs text-muted-foreground uppercase">
                <tr><th class="py-2">Name</th><th>Roles</th><th>Status</th><th>Last seen</th></tr>
              </thead>
              <tbody>
                @for (n of nodes(); track n.id) {
                  <tr class="border-t">
                    <td class="py-2">{{ n.name }}</td>
                    <td>{{ n.roles.join(', ') }}</td>
                    <td>{{ n.status }}</td>
                    <td>{{ n.lastSeenAt ? (n.lastSeenAt | date:'short') : '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </section>
    </div>
  `,
})
export class NodesComponent implements OnInit {
  private svc = inject(NodesService);
  nodes = signal<NodeDto[]>([]);
  key = signal<ProvisioningKeyResult | null>(null);

  private intervalId: ReturnType<typeof setInterval> | undefined;

  ngOnInit() {
    this.refresh();
    this.intervalId = setInterval(() => this.refresh(), 5000);
  }

  refresh() {
    this.svc.list().subscribe(x => this.nodes.set(x));
  }

  createKey() {
    this.svc.createKey(['docker']).subscribe(x => this.key.set(x));
  }
}
