import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  Api, CATEGORIES, ClientCompany, NewTicket, PRIORITIES, STATUSES, Page, Ticket, TicketFilter, errorMessage, label,
} from './api';
import { Auth } from './auth';

@Component({
  selector: 'app-tickets',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <section class="toolbar">
      <h1>Tickets</h1>
      <select name="status" [(ngModel)]="filter.status" (ngModelChange)="search(0)" aria-label="Status">
        <option value="">All statuses</option>
        @for (s of statuses; track s) { <option [value]="s">{{ label(s) }}</option> }
      </select>
      <select name="priority" [(ngModel)]="filter.priority" (ngModelChange)="search(0)" aria-label="Priority">
        <option value="">All priorities</option>
        @for (p of priorities; track p) { <option [value]="p">{{ label(p) }}</option> }
      </select>
      @if (canCreate) {
        <button (click)="showForm.set(!showForm())">{{ showForm() ? 'Cancel' : 'New ticket' }}</button>
      }
    </section>

    @if (showForm()) {
      <form class="card" #form="ngForm" (ngSubmit)="create()">
        <label>Title <input name="title" [(ngModel)]="draft.title" required maxlength="255" /></label>
        <label>Description <textarea name="description" [(ngModel)]="draft.description" required rows="3"></textarea></label>
        <div class="row">
          <label>Priority
            <select name="draftPriority" [(ngModel)]="draft.priority">
              @for (p of priorities; track p) { <option [value]="p">{{ label(p) }}</option> }
            </select>
          </label>
          <label>Category
            <select name="draftCategory" [(ngModel)]="draft.category">
              @for (c of categories; track c) { <option [value]="c">{{ label(c) }}</option> }
            </select>
          </label>
          @if (auth.isStaff()) {
            <label>Client
              <select name="draftClient" [(ngModel)]="draft.clientId" required>
                @for (c of clients(); track c.id) { <option [value]="c.id">{{ c.companyName }}</option> }
              </select>
            </label>
          }
        </div>
        <button type="submit" [disabled]="form.invalid">Create</button>
      </form>
    }

    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (page(); as p) {
      <table>
        <thead>
          <tr><th>Title</th><th>Client</th><th>Status</th><th>Priority</th><th>Assignee</th><th>Comments</th><th>Created</th></tr>
        </thead>
        <tbody>
          @for (t of p.content; track t.id) {
            <tr>
              <td><a [routerLink]="['/tickets', t.id]">{{ t.title }}</a></td>
              <td>{{ t.client.companyName }}</td>
              <td><span class="badge" [attr.data-status]="t.status">{{ label(t.status) }}</span></td>
              <td><span class="badge" [attr.data-priority]="t.priority">{{ label(t.priority) }}</span></td>
              <td>{{ t.assignee?.name ?? '—' }}</td>
              <td>{{ t.commentCount }}</td>
              <td>{{ t.createdAt | date: 'MMM d, HH:mm' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="empty">No tickets match.</td></tr>
          }
        </tbody>
      </table>
      <nav class="pager">
        <button [disabled]="p.page === 0" (click)="search(p.page - 1)">Previous</button>
        <span>Page {{ p.page + 1 }} of {{ p.totalPages || 1 }} · {{ p.totalElements }} tickets</span>
        <button [disabled]="p.page + 1 >= p.totalPages" (click)="search(p.page + 1)">Next</button>
      </nav>
    }
  `,
})
export class Tickets implements OnInit {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);

  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly categories = CATEGORIES;
  protected readonly label = label;
  protected readonly canCreate = this.auth.isStaff() || this.auth.isClient();

  readonly page = signal<Page<Ticket> | null>(null);
  readonly clients = signal<ClientCompany[]>([]);
  readonly showForm = signal(false);
  readonly error = signal('');

  filter: TicketFilter = { status: '', priority: '', page: 0, size: 10 };
  draft: NewTicket = this.emptyDraft();

  ngOnInit(): void {
    this.search(0);
    if (this.auth.isStaff()) this.api.clients().subscribe((c) => this.clients.set(c));
  }

  search(page: number): void {
    this.filter.page = page;
    this.api.tickets(this.filter).subscribe({
      next: (p) => this.page.set(p),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  create(): void {
    this.error.set('');
    this.api.createTicket(this.draft).subscribe({
      next: () => {
        this.draft = this.emptyDraft();
        this.showForm.set(false);
        this.search(0);
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private emptyDraft(): NewTicket {
    return { title: '', description: '', priority: 'MEDIUM', category: 'SUPPORT' };
  }
}
