import { Component, OnInit, OnDestroy, inject, signal, ElementRef, ViewChild, AfterViewChecked } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCard, HlmCardContent, HlmCardHeader, HlmCardTitle, HlmCardDescription } from '@spartan-ng/helm/card';
import { DeploymentsService, DeploymentDto } from './deployments.service';

const TERMINAL_STATUSES = ['Succeeded', 'Failed', 'RolledBack', 'Cancelled'];

@Component({
  selector: 'limen-deployment-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, HlmButton, HlmCard, HlmCardContent, HlmCardHeader, HlmCardTitle, HlmCardDescription],
  templateUrl: './deployment-detail.html',
})
export class DeploymentDetail implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('logContainer') logContainer?: ElementRef<HTMLElement>;

  private deploymentsService = inject(DeploymentsService);
  private route = inject(ActivatedRoute);

  id = '';
  deployment = signal<DeploymentDto | null>(null);
  logLines = signal<string[]>([]);
  private pollInterval: ReturnType<typeof setInterval> | null = null;
  private shouldScrollToBottom = false;

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.fetchDeployment();
    this.startPolling();
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  ngAfterViewChecked() {
    if (this.shouldScrollToBottom && this.logContainer) {
      const el = this.logContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
      this.shouldScrollToBottom = false;
    }
  }

  private fetchDeployment() {
    this.deploymentsService.list().subscribe(list => {
      const found = list.find(d => d.id === this.id) ?? null;
      this.deployment.set(found);
      if (found && TERMINAL_STATUSES.includes(found.status)) {
        this.stopPolling();
      }
    });
  }

  private fetchLogs() {
    this.deploymentsService.logs(this.id).subscribe({
      next: text => {
        const lines = text.split('\n').filter(l => l.trim().length > 0);
        this.logLines.set(lines);
        this.shouldScrollToBottom = true;
      },
      error: () => {},
    });
  }

  private startPolling() {
    this.fetchLogs();
    this.pollInterval = setInterval(() => {
      this.fetchDeployment();
      this.fetchLogs();
    }, 2000);
  }

  private stopPolling() {
    if (this.pollInterval !== null) {
      clearInterval(this.pollInterval);
      this.pollInterval = null;
    }
  }

  parseLogLine(line: string): string {
    try {
      const obj = JSON.parse(line);
      const stage = obj.stage ?? obj.Stage ?? '';
      const message = obj.message ?? obj.Message ?? line;
      const percent = obj.percent ?? obj.Percent ?? obj.progress ?? obj.Progress;
      const stagePrefix = stage ? `[${stage}] ` : '';
      const percentSuffix = percent !== undefined && percent !== null ? ` (${percent}%)` : '';
      return `${stagePrefix}${message}${percentSuffix}`;
    } catch {
      return line;
    }
  }

  isTerminal(status: string): boolean {
    return TERMINAL_STATUSES.includes(status);
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

  cancel() {
    this.deploymentsService.cancel(this.id).subscribe(() => this.fetchDeployment());
  }
}
