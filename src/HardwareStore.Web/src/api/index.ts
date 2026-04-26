import api from './client';
import type {
  LoginResponse,
  ParseQueryResponse,
  ProductSelection,
  SearchStatus,
  SearchReport,
  ReportSummary,
  UserDto,
  Retailer,
} from '../types';

// Auth
export const signup = (email: string, displayName: string, password: string) =>
  api.post('/auth/signup', { email, displayName, password });

export const login = (email: string, password: string) =>
  api.post<LoginResponse>('/auth/login', { email, password });

// Public search
export const publicSearch = (query: string) =>
  api.post('/public/search', { query });

export const getPublicSearchStatus = (id: string) =>
  api.get<SearchStatus>(`/public/search/${id}/status`);

// Authenticated search
export const parseQuery = (query: string) =>
  api.post<ParseQueryResponse>('/search/parse', { query });

export const createSearch = (payload: {
  naturalLanguageQuery: string;
  selectedProducts: ProductSelection[];
  additionalItems: ProductSelection[];
  selectedRetailerIds: string[];
}) => api.post<{ searchRequestId: string; status: string }>('/search', payload);

export const getSearchStatus = (id: string) =>
  api.get<SearchStatus>(`/search/${id}/status`);

// Reports
export const getReports = () =>
  api.get<ReportSummary[]>('/reports');

export const getReport = (id: string) =>
  api.get<SearchReport>(`/reports/${id}`);

// Admin
export const adminGetUsers = () =>
  api.get<UserDto[]>('/admin/users');

export const adminGetPendingUsers = () =>
  api.get<UserDto[]>('/admin/users/pending');

export const adminApproveUser = (id: string) =>
  api.put<UserDto>(`/admin/users/${id}/approve`);

export const adminRejectUser = (id: string) =>
  api.put<UserDto>(`/admin/users/${id}/reject`);

export const adminUpdateUserSettings = (
  id: string,
  settings: { dailySearchLimit: number; allowedRetailerIds: string[]; status?: string }
) => api.put<UserDto>(`/admin/users/${id}/settings`, settings);

export const adminDeleteUser = (id: string) =>
  api.delete(`/admin/users/${id}`);

export const adminGetRetailers = () =>
  api.get<Retailer[]>('/admin/retailers');

export const adminCreateRetailer = (retailer: Omit<Retailer, 'id'>) =>
  api.post<Retailer>('/admin/retailers', retailer);

export const adminUpdateRetailer = (id: string, retailer: Omit<Retailer, 'id'>) =>
  api.put<Retailer>(`/admin/retailers/${id}`, retailer);

export const adminDeleteRetailer = (id: string) =>
  api.delete(`/admin/retailers/${id}`);
