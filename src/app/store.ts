import { Injectable, signal, inject } from '@angular/core';
import { type State, type Order, type Material, type Product, type Movement, type Expense, type User, type Role, type Attendance } from './domain';
import { ApiService } from './api.service';

const STORAGE_KEY = 'floreria_lacarreta_state_v2';

export function createEmptyState(): State {
  return {
    version: 1,
    business: 'Floristería La Carreta',
    orders: [],
    materials: [],
    products: [],
    movements: [],
    expenses: [],
    users: [],
    roles: [],
    attendances: []
  };
}

@Injectable({ providedIn: 'root' })
export class AppStore {
  private readonly api = inject(ApiService);
  readonly state = signal<State>(createEmptyState());
  readonly loading = signal<boolean>(false);
  readonly error = signal<string>('');
  readonly warning = signal<string>('');
  readonly isConnectedToDb = signal<boolean>(true);

  constructor() {
    // Si ya existe sesión activa, cargar inmediatamente los datos de la base de datos
    if (this.api.isLoggedIn) {
      this.refreshFromDatabase();
    }
  }

  async refreshFromDatabase(): Promise<void> {
    this.loading.set(true);
    this.error.set('');

    try {
      const [orders, products, materials, movements, users, roles, attendances, expenses] = await Promise.all([
        this.api.getOrders().catch(() => []),
        this.api.getProducts().catch(() => []),
        this.api.getMaterials().catch(() => []),
        this.api.getMovements().catch(() => []),
        this.api.getUsers().catch(() => []),
        this.api.getRoles().catch(() => []),
        this.api.getAttendances().catch(() => []),
        this.api.getExpenses().catch(() => [])
      ]);

      const newState: State = {
        version: 1,
        business: 'Floristería La Carreta',
        orders,
        products,
        materials,
        movements,
        users,
        roles,
        attendances,
        expenses
      };

      this.state.set(newState);
      this.isConnectedToDb.set(true);
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(newState));
      } catch { }
    } catch (err) {
      this.isConnectedToDb.set(false);
      this.error.set('No se pudo conectar a la base de datos MySQL.');
    } finally {
      this.loading.set(false);
    }
  }

  commit(next: State) {
    this.state.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch { }
  }

  reset() {
    this.refreshFromDatabase();
  }

  clear() {
    this.state.set(createEmptyState());
    this.error.set('');
    this.warning.set('');
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch { }
  }
}

// Alias for compatibility
export { AppStore as DemoStore };
