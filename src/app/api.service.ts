import { Injectable } from '@angular/core';
import { buildExcelXml } from './domain';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private baseUrl = 'http://localhost:5000/api';
  private token: string | null = null;

  constructor() {
    this.token = localStorage.getItem('flore_token');
  }

  setToken(t: string | null) {
    this.token = t;
    if (t) localStorage.setItem('flore_token', t);
    else localStorage.removeItem('flore_token');
  }

  async checkHealth(): Promise<boolean> {
    try {
      const res = await fetch(`${this.baseUrl}/products`, { method: 'GET', headers: this.headers() });
      return res.ok;
    } catch {
      return false;
    }
  }

  private headers(): HeadersInit {
    const h: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.token) h['Authorization'] = `Bearer ${this.token}`;
    return h;
  }

  // --- EXPORTACIONES EXCEL ---
  async downloadExcelFromBackend(endpoint: 'orders' | 'attendance' | 'inventory' | 'financial', filename: string): Promise<boolean> {
    try {
      const res = await fetch(`${this.baseUrl}/export/${endpoint}`, { headers: this.headers() });
      if (!res.ok) throw new Error('API unavailable');
      const blob = await res.blob();
      this.triggerDownload(blob, filename);
      return true;
    } catch {
      return false; // Fallback to frontend XML spreadsheet
    }
  }

  downloadExcelLocal(sheets: { name: string; headers: string[]; rows: (string | number | boolean)[][] }[], filename: string) {
    const xml = buildExcelXml(sheets);
    const blob = new Blob([xml], { type: 'application/vnd.ms-excel;charset=utf-8' });
    this.triggerDownload(blob, filename.endsWith('.xls') || filename.endsWith('.xlsx') ? filename : filename + '.xls');
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
