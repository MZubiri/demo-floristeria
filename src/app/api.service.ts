import { Injectable } from '@angular/core';
import {
  type Order, type Line, type Payment, type Product, type Material,
  type Movement, type Expense, type User, type Role, type Attendance,
  buildExcelXml
} from './domain';

export interface LoginResponseDto {
  token: string;
  user: User;
}

export interface SyncResult {
  success: boolean;
  message: string;
  productsSynced: number;
  ordersSynced: number;
  timestamp: string;
}

export interface SyncStatus {
  isConnected: boolean;
  webShopUrl: string;
  lastSync?: string;
  totalOrders: number;
  totalProducts: number;
  statusMessage: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly baseUrl = 'http://localhost:5000/api';
  private token: string | null = null;
  private currentUser: User | null = null;

  constructor() {
    this.token = localStorage.getItem('lacarreta_token');
    const u = localStorage.getItem('lacarreta_user');
    if (u) {
      try { this.currentUser = JSON.parse(u); } catch { this.currentUser = null; }
    }
  }

  get isLoggedIn(): boolean {
    return !!this.token;
  }

  get user(): User | null {
    return this.currentUser;
  }

  setSession(token: string, user: User) {
    this.token = token;
    this.currentUser = user;
    localStorage.setItem('lacarreta_token', token);
    localStorage.setItem('lacarreta_user', JSON.stringify(user));
  }

  clearSession() {
    this.token = null;
    this.currentUser = null;
    localStorage.removeItem('lacarreta_token');
    localStorage.removeItem('lacarreta_user');
  }

  private headers(): HeadersInit {
    const h: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.token) {
      h['Authorization'] = `Bearer ${this.token}`;
    }
    return h;
  }

  private async request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      ...options,
      headers: { ...this.headers(), ...(options.headers as Record<string, string>) }
    });

    if (!res.ok) {
      let errMsg = `Error ${res.status}: ${res.statusText}`;
      try {
        const errObj = await res.json();
        if (errObj.message) errMsg = errObj.message;
      } catch { }
      throw new Error(errMsg);
    }

    if (res.status === 204) {
      return {} as T;
    }

    return res.json() as Promise<T>;
  }

  // ==========================================
  // AUTENTICACIÓN
  // ==========================================
  async login(email: string, password: string): Promise<LoginResponseDto> {
    const res = await fetch(`${this.baseUrl}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (!res.ok) {
      let msg = 'Credenciales incorrectas o usuario no encontrado.';
      try {
        const errObj = await res.json();
        if (errObj.message) msg = errObj.message;
      } catch { }
      throw new Error(msg);
    }

    const data = await res.json();
    this.setSession(data.token, data.user);
    return data;
  }

  // ==========================================
  // PEDIDOS Y VENTAS
  // ==========================================
  async getOrders(params?: { query?: string; status?: string; date?: string; pendingOnly?: boolean }): Promise<Order[]> {
    const query = new URLSearchParams();
    if (params?.query) query.set('query', params.query);
    if (params?.status) query.set('status', params.status);
    if (params?.date) query.set('date', params.date);
    if (params?.pendingOnly) query.set('pendingOnly', 'true');
    const qs = query.toString();
    return this.request<Order[]>(`/orders${qs ? '?' + qs : ''}`);
  }

  async getOrder(id: string): Promise<Order> {
    return this.request<Order>(`/orders/${id}`);
  }

  async createOrder(order: Partial<Order>): Promise<Order> {
    return this.request<Order>('/orders', {
      method: 'POST',
      body: JSON.stringify(order)
    });
  }

  async createDirectSale(dto: {
    customer: string;
    phone: string;
    items: { productId: string; name: string; quantity: number; price: number }[];
    paymentMethod: string;
    discount: number;
    reference: string;
    notes: string;
  }): Promise<Order> {
    return this.request<Order>('/orders/direct-sale', {
      method: 'POST',
      body: JSON.stringify(dto)
    });
  }

  async updateOrderStatus(id: string, status: string, note?: string, receivedBy?: string): Promise<Order> {
    return this.request<Order>(`/orders/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status, note, receivedBy })
    });
  }

  async addPayment(orderId: string, payment: { amount: number; method: string; reference: string }): Promise<Payment> {
    return this.request<Payment>(`/orders/${orderId}/payments`, {
      method: 'POST',
      body: JSON.stringify(payment)
    });
  }

  // ==========================================
  // PRODUCTOS Y CATÁLOGO
  // ==========================================
  async getProducts(): Promise<Product[]> {
    return this.request<Product[]>('/products');
  }

  async createProduct(p: any): Promise<Product> {
    return this.request<Product>('/products', {
      method: 'POST',
      body: JSON.stringify(p)
    });
  }

  async updateProduct(id: string, p: any): Promise<Product> {
    return this.request<Product>(`/products/${id}`, {
      method: 'PUT',
      body: JSON.stringify(p)
    });
  }

  async deleteProduct(id: string): Promise<any> {
    return this.request<any>(`/products/${id}`, {
      method: 'DELETE'
    });
  }

  async uploadImage(file: File): Promise<{ url: string; fileName: string; message: string }> {
    const formData = new FormData();
    formData.append('file', file);
    const headers: Record<string, string> = {};
    if (this.token) headers['Authorization'] = `Bearer ${this.token}`;
    const res = await fetch(`${this.baseUrl}/upload`, {
      method: 'POST',
      headers,
      body: formData
    });
    if (!res.ok) {
      let msg = 'Error al subir la fotografía.';
      try {
        const err = await res.json();
        if (err.message) msg = err.message;
      } catch { }
      throw new Error(msg);
    }
    return res.json();
  }

  // ==========================================
  // INVENTARIO Y MATERIALES
  // ==========================================
  async getMaterials(): Promise<Material[]> {
    return this.request<Material[]>('/inventory/materials');
  }

  async getMovements(): Promise<Movement[]> {
    return this.request<Movement[]>('/inventory/movements');
  }

  async createMovement(m: { materialId: string; type: string; quantity: number; cost?: number; reason: string }): Promise<any> {
    return this.request<any>('/inventory/movements', {
      method: 'POST',
      body: JSON.stringify(m)
    });
  }

  // ==========================================
  // ASISTENCIA Y PASE DE LISTA
  // ==========================================
  async getAttendances(date?: string): Promise<Attendance[]> {
    const qs = date ? `?date=${date}` : '';
    return this.request<Attendance[]>(`/attendance${qs}`);
  }

  async markAttendance(data: {
    userId: number;
    date: string;
    clockIn?: string;
    clockOut?: string;
    status: string;
    notes?: string;
  }): Promise<Attendance> {
    return this.request<Attendance>('/attendance', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  // ==========================================
  // USUARIOS Y ROLES (CRUD)
  // ==========================================
  async getUsers(): Promise<User[]> {
    return this.request<User[]>('/users');
  }

  async createUser(u: any): Promise<User> {
    return this.request<User>('/users', {
      method: 'POST',
      body: JSON.stringify(u)
    });
  }

  async updateUser(id: number, u: any): Promise<User> {
    return this.request<User>(`/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify(u)
    });
  }

  async toggleUserStatus(id: number): Promise<any> {
    return this.request<any>(`/users/${id}/toggle-status`, {
      method: 'PATCH'
    });
  }

  async deleteUser(id: number): Promise<any> {
    return this.request<any>(`/users/${id}`, {
      method: 'DELETE'
    });
  }

  async getRoles(): Promise<Role[]> {
    return this.request<Role[]>('/roles');
  }

  async createRole(r: any): Promise<Role> {
    return this.request<Role>('/roles', {
      method: 'POST',
      body: JSON.stringify(r)
    });
  }

  async updateRole(id: number, r: any): Promise<Role> {
    return this.request<Role>(`/roles/${id}`, {
      method: 'PUT',
      body: JSON.stringify(r)
    });
  }

  // ==========================================
  // GASTOS
  // ==========================================
  async getExpenses(from?: string, to?: string): Promise<Expense[]> {
    const q = new URLSearchParams();
    if (from) q.set('from', from);
    if (to) q.set('to', to);
    const qs = q.toString();
    return this.request<Expense[]>(`/expenses${qs ? '?' + qs : ''}`);
  }

  async createExpense(exp: Partial<Expense>): Promise<Expense> {
    return this.request<Expense>('/expenses', {
      method: 'POST',
      body: JSON.stringify(exp)
    });
  }

  async deleteExpense(id: string): Promise<any> {
    return this.request<any>(`/expenses/${id}`, {
      method: 'DELETE'
    });
  }

  // ==========================================
  // SINCRONIZACIÓN CON TIENDA WEB
  // ==========================================
  async syncWeb(): Promise<SyncResult> {
    return this.request<SyncResult>('/sync/all', {
      method: 'POST'
    });
  }

  async getSyncStatus(): Promise<SyncStatus> {
    return this.request<SyncStatus>('/sync/status');
  }

  // ==========================================
  // REPORTES Y EXPORTACIÓN EXCEL
  // ==========================================
  async downloadExcelFromBackend(endpoint: 'orders' | 'attendance' | 'inventory' | 'financial', filename: string): Promise<boolean> {
    try {
      const res = await fetch(`${this.baseUrl}/export/${endpoint}`, { headers: this.headers() });
      if (!res.ok) throw new Error('API unavailable');
      const blob = await res.blob();
      this.triggerDownload(blob, filename);
      return true;
    } catch {
      return false;
    }
  }

  downloadExcelLocal(sheets: { name: string; headers: string[]; rows: (string | number | boolean)[][] }[], filename: string) {
    const xml = buildExcelXml(sheets);
    const blob = new Blob([xml], { type: 'application/vnd.ms-excel;charset=utf-8' });
    this.triggerDownload(blob, filename.endsWith('.xls') || filename.endsWith('.xlsx') ? filename : filename + '.xls');
  }

  // ==========================================
  // FOTO REAL DE ARREGLO FINAL
  // ==========================================
  async setOrderFinalPhoto(orderId: string, photoUrl: string): Promise<Order> {
    return this.request<Order>(`/orders/${orderId}/final-photo`, {
      method: 'POST',
      body: JSON.stringify({ photoUrl })
    });
  }

  // ==========================================
  // ARQUEO Y CIERRE DE CAJA (POS)
  // ==========================================
  async getCashRegisterSummary(date?: string): Promise<any> {
    const q = date ? `?date=${encodeURIComponent(date)}` : '';
    return this.request<any>(`/cash-register/summary${q}`);
  }

  async closeCashRegister(payload: { date: string; openingBalance: number; actualCash: number; cashierName: string; notes?: string }): Promise<any> {
    return this.request<any>('/cash-register/close', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }

  async getCashRegisterHistory(): Promise<any[]> {
    return this.request<any[]>('/cash-register/history');
  }

  // ==========================================
  // COPIAS DE SEGURIDAD MYSQL
  // ==========================================
  async getBackups(): Promise<any[]> {
    return this.request<any[]>('/backup/list');
  }

  async createBackupNow(): Promise<any> {
    return this.request<any>('/backup/now', {
      method: 'POST'
    });
  }

  downloadBackupFile(fileName: string) {
    const url = `${this.baseUrl}/backup/download/${encodeURIComponent(fileName)}`;
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  private triggerDownload(blob: Blob, name: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
