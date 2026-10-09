import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, Comment, STATUSES, Status, Ticket, errorMessage, label } from './api';
import { Auth } from './auth';

@Component({
  selector: 'app-ticket-detail',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <p><a routerLink="/tickets">← All tickets</a></p>
    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (ticket(); as t) {
      <article class="card">
        <h1>{{ t.title }}</h1>
        <p class="meta">
          {{ t.client.companyName }} · {{ label(t.category) }} ·
          <span class="badge" [attr.data-priority]="t.priority">{{ label(t.priority) }}</span> ·
          opened by {{ t.createdBy?.name ?? 'unknown' }} {{ t.createdAt | date: 'MMM d, y HH:mm' }}
        </p>
        <p class="description">{{ t.description }}</p>
        <p>
          Status:
          @if (canChangeStatus) {
            <select name="status" [ngModel]="t.status" (ngModelChange)="setStatus($event)" aria-label="Status">
              @for (s of statuses; track s) { <option [value]="s">{{ label(s) }}</option> }
            </select>
          } @else {
            <span class="badge" [attr.data-status]="t.status">{{ label(t.status) }}</span>
          }
          · Assignee: {{ t.assignee?.name ?? 'unassigned' }}
          @if (t.resolvedAt) { · Resolved {{ t.resolvedAt | date: 'MMM d, HH:mm' }} }
        </p>
      </article>

      <h2>Comments</h2>
      @for (c of comments(); track c.id) {
        <div class="comment" [class.internal]="c.internal">
          <strong>{{ c.author?.name ?? 'unknown' }}</strong>
          <span class="meta">{{ c.createdAt | date: 'MMM d, HH:mm' }}@if (c.internal) { · internal note }</span>
          <p>{{ c.content }}</p>
        </div>
      } @empty {
        <p class="empty">No comments yet.</p>
      }

      <form class="card" (ngSubmit)="addComment()">
        <label>Add a comment <textarea name="comment" [(ngModel)]="newComment" required rows="3"></textarea></label>
        @if (!auth.isClient()) {
          <label class="inline"><input type="checkbox" name="internal" [(ngModel)]="internal" /> Internal note (hidden from the client)</label>
        }
        <button type="submit" [disabled]="!newComment.trim()">Post</button>
      </form>
    }
  `,
})
export class TicketDetail implements OnInit {
  private readonly api = inject(Api);
  protected readonly auth = inject(Auth);

  // Bound from the :id route parameter (withComponentInputBinding).
  readonly id = input.required<string>();

  protected readonly statuses = STATUSES;
  protected readonly label = label;
  protected readonly canChangeStatus = !this.auth.isClient();

  readonly ticket = signal<Ticket | null>(null);
  readonly comments = signal<Comment[]>([]);
  readonly error = signal('');

  newComment = '';
  internal = false;

  ngOnInit(): void {
    this.api.ticket(this.id()).subscribe({
      next: (t) => this.ticket.set(t),
      error: (err) => this.error.set(errorMessage(err)),
    });
    this.loadComments();
  }

  setStatus(status: Status): void {
    this.api.updateStatus(this.id(), status).subscribe({
      next: (t) => this.ticket.set(t),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  addComment(): void {
    this.api.addComment(this.id(), this.newComment, this.internal).subscribe({
      next: () => {
        this.newComment = '';
        this.internal = false;
        this.loadComments();
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  private loadComments(): void {
    this.api.comments(this.id()).subscribe({
      next: (c) => this.comments.set(c),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
