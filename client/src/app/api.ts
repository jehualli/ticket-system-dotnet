import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// Shapes returned by the ASP.NET Core API (see Dtos.cs).

export type Role = 'ADMIN' | 'AGENT' | 'DEVELOPER' | 'CLIENT';
export type Status = 'OPEN' | 'IN_PROGRESS' | 'PENDING' | 'RESOLVED' | 'CLOSED';
export type Priority = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
export type Category = 'BUG' | 'FEATURE' | 'SUPPORT' | 'BILLING' | 'OTHER';

export const STATUSES: Status[] = ['OPEN', 'IN_PROGRESS', 'PENDING', 'RESOLVED', 'CLOSED'];
export const PRIORITIES: Priority[] = ['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'];
export const CATEGORIES: Category[] = ['BUG', 'FEATURE', 'SUPPORT', 'BILLING', 'OTHER'];

export interface AuthResponse {
  token: string;
  userId: string;
  name: string;
  email: string;
  role: Role;
}

export interface UserSummary {
  id: string;
  name: string;
  email: string;
}

export interface Ticket {
  id: string;
  title: string;
  description: string;
  status: Status;
  priority: Priority;
  category: Category;
  client: { id: string; companyName: string };
  assignee: UserSummary | null;
  createdBy: UserSummary | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  commentCount: number;
}

export interface Page<T> {
  content: T[];
  page: number;
  size: number;
  totalElements: number;
  totalPages: number;
}

export interface Comment {
  id: string;
  author: UserSummary | null;
  content: string;
  internal: boolean;
  createdAt: string;
}

export interface ClientCompany {
  id: string;
  companyName: string;
}

export interface TicketFilter {
  status?: Status | '';
  priority?: Priority | '';
  page: number;
  size: number;
}

export interface NewTicket {
  title: string;
  description: string;
  priority: Priority;
  category: Category;
  clientId?: string;
}

export interface ApiError {
  status: number;
  message: string;
  fieldErrors?: Record<string, string[]>;
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password });
  }

  tickets(filter: TicketFilter): Observable<Page<Ticket>> {
    let params = new HttpParams().set('page', filter.page).set('size', filter.size);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.priority) params = params.set('priority', filter.priority);
    return this.http.get<Page<Ticket>>('/api/tickets', { params });
  }

  ticket(id: string): Observable<Ticket> {
    return this.http.get<Ticket>(`/api/tickets/${id}`);
  }

  createTicket(ticket: NewTicket): Observable<Ticket> {
    return this.http.post<Ticket>('/api/tickets', ticket);
  }

  updateStatus(id: string, status: Status): Observable<Ticket> {
    return this.http.put<Ticket>(`/api/tickets/${id}`, { status });
  }

  comments(ticketId: string): Observable<Comment[]> {
    return this.http.get<Comment[]>(`/api/tickets/${ticketId}/comments`);
  }

  addComment(ticketId: string, content: string, internal: boolean): Observable<Comment> {
    return this.http.post<Comment>(`/api/tickets/${ticketId}/comments`, { content, internal });
  }

  clients(): Observable<ClientCompany[]> {
    return this.http.get<ClientCompany[]>('/api/clients');
  }
}

// Turns an HttpErrorResponse into the API's message, or a generic one if the server sent none.
export function errorMessage(err: { status?: number; error?: Partial<ApiError> | null }): string {
  const fields = err.error?.fieldErrors;
  if (fields) {
    return Object.entries(fields)
      .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
      .join('; ');
  }
  return err.error?.message ?? `Request failed (${err.status ?? 'network error'})`;
}

export function label(value: string): string {
  return value.replace('_', ' ').toLowerCase();
}
