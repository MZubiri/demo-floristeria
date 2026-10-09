import { Component, computed, HostListener, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IconComponent } from './icon.component';
import { DialogComponent } from './dialog.component';
import { PhotosComponent, clearPhotos } from './photos.component';
import { OrderFormComponent } from './order-form.component';
import { DemoStore } from './store';
import { ApiService } from './api.service';
import {
  type Order, type OrderStatus, type Product, type Material, type User, type Role, type Attendance,
  STATUSES, METHODS, total, paid, balance, orderCost, paymentLabel, nextStatus,
  reserved, available, dayKey, offsetDay, blankOrder, newLine, saveOrder, addPayment,
  transition, moveStock, summary, csvCell, uid, directSale, markAttendance, saveUser,
  toggleUserStatus, deleteUser, saveRole
} from './domain';

type View = 'inicio' | 'pedidos' | 'agenda' | 'catalogo' | 'inventario' | 'asistencia' | 'usuarios' | 'pagos' | 'gastos' | 'contactos' | 'informes' | 'ajustes';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, IconComponent, DialogComponent, PhotosComponent, OrderFormComponent],
  templateUrl: './app.component.html'
})
export class AppComponent {
  readonly store = inject(DemoStore);
  readonly apiService = inject(ApiService);
  readonly state = this.store.state;

  readonly navItems: { id: View; label: string; icon: string; group: string }[] = [
    { id: 'inicio', label: 'Inicio', icon: 'home', group: 'TU FLORISTERÍA' },
    { id: 'pedidos', label: 'Pedidos', icon: 'orders', group: '' },
    { id: 'agenda', label: 'Agenda de entregas', icon: 'calendar', group: '' },
    { id: 'catalogo', label: 'Catálogo floral', icon: 'flower', group: '' },
    { id: 'inventario', label: 'Inventario y merma', icon: 'stock', group: '' },
    { id: 'asistencia', label: 'Pase de lista', icon: 'clipboard', group: 'EQUIPO Y GESTIÓN' },
    { id: 'usuarios', label: 'Usuarios y roles', icon: 'users', group: '' },
    { id: 'pagos', label: 'Ventas y pagos', icon: 'wallet', group: 'TU NEGOCIO' },
    { id: 'gastos', label: 'Gastos', icon: 'expense', group: '' },
    { id: 'contactos', label: 'Clientes y proveedores', icon: 'users', group: '' },
    { id: 'informes', label: 'Informes', icon: 'chart', group: '' },
    { id: 'ajustes', label: 'Configuración', icon: 'settings', group: '' }
  ];

  readonly view = signal<View>('inicio');
  menuOpen = false;
  query = '';
  dateFilter = 'all';
  statusFilter = '';
  pendingOnly = false;

  readonly selectedId = signal('');
  readonly selected = computed(() => this.state().orders.find(o => o.id === this.selectedId()));
  readonly draft = signal<Order | null>(null);
  editing = false;
  detailTab = 'resumen';
  error = signal('');
  toast = signal('');
  private toastTimer?: ReturnType<typeof setTimeout>;

  total = total;
  paid = paid;
  balance = balance;
  orderCost = orderCost;
  paymentLabel = paymentLabel;
  nextStatus = nextStatus;
  statuses = STATUSES;
  methods = METHODS;

  today = dayKey();
  readonly daily = computed(() => summary(this.state(), this.today, this.today));
  readonly week = computed(() => summary(this.state(), offsetDay(-6), this.today));
  readonly activeOrders = computed(() => this.state().orders.filter(o => !['entregado', 'cancelado'].includes(o.status)));
  readonly lowStock = computed(() => this.state().materials.filter(m => available(this.state(), m.id) <= m.minimum));
  readonly due = computed(() => this.activeOrders().filter(o => o.deliveryDate === this.today).sort((a, b) => a.time.localeCompare(b.time)));
  readonly receivables = computed(() => this.state().orders.filter(o => o.status !== 'cancelado').reduce((n, o) => n + balance(o), 0));

  detailAmount = 0;
  detailMethod = 'Nequi';
  detailReference = '';
  receipt = '';
  deliveryNote = '';

  calendarMonth = new Date(new Date().getFullYear(), new Date().getMonth(), 1);
  calendarDay = dayKey();

  stockQuery = '';
  stockCategory = '';
  onlyLow = false;
  stockTab = 'materiales';
  stockModal = false;
  movement = { materialId: 'rosa', type: 'merma' as 'entrada' | 'merma', quantity: 1, cost: 3000, reason: '' };

  expenseModal = false;
  expense = { category: 'Domicilios', description: '', amount: 0, date: dayKey(), method: 'Efectivo' };
  readonly expenseCategories = ['Domicilios', 'Transporte', 'Trabajadores', 'Servicios', 'Arriendo', 'Publicidad', 'Mantenimiento', 'Otros'];

  contactTab = 'clientes';
  contactQuery = '';
  catalogQuery = '';
  catalogCategory = '';

  reportFrom = offsetDay(-29);
  reportTo = dayKey();
  paymentQuery = '';
  onlyBalance = true;
  expenseFrom = offsetDay(-29);
  expenseTo = dayKey();

  // ==========================================
  // VENTA DIRECTA EN LOCAL (PUNTO DE VENTA / POS)
  // ==========================================
  posModal = false;
  posCustomer = 'Cliente Mostrador';
  posPhone = '';
  posPaymentMethod = 'Efectivo';
  posDiscount = 0;
  posReference = 'Venta en tienda física';
  posNotes = '';
  posSelectedProductId = '';
  posItems: { product: Product; quantity: number; price: number }[] = [];

  openPos() {
    this.posModal = true;
    this.posCustomer = 'Cliente Mostrador';
    this.posPhone = '';
    this.posPaymentMethod = 'Efectivo';
    this.posDiscount = 0;
    this.posReference = 'Venta directa mostrador';
    this.posNotes = '';
    this.error.set('');
    if (this.state().products.length) {
      this.posSelectedProductId = this.state().products[0].id;
      if (!this.posItems.length) {
        this.posItems = [{ product: this.state().products[0], quantity: 1, price: this.state().products[0].price }];
      }
    }
  }

  addPosSelected() {
    const p = this.state().products.find(x => x.id === this.posSelectedProductId);
    if (p) this.addPosProduct(p);
  }

  addPosProduct(p: Product) {
    const item = this.posItems.find(i => i.product.id === p.id);
    if (item) {
      item.quantity++;
    } else {
      this.posItems.push({ product: p, quantity: 1, price: p.price });
    }
  }

  removePosItem(idx: number) {
    this.posItems.splice(idx, 1);
  }

  get posSubtotal() {
    return this.posItems.reduce((acc, i) => acc + i.price * i.quantity, 0);
  }

  get posTotal() {
    return Math.max(0, this.posSubtotal - (this.posDiscount || 0));
  }

  confirmPosSale() {
    try {
      if (!this.posItems.length) throw new Error('Agrega al menos un arreglo o producto a la venta.');
      const { next, order } = directSale(this.state(), {
        customer: this.posCustomer,
        phone: this.posPhone,
        items: this.posItems,
        paymentMethod: this.posPaymentMethod,
        discount: Number(this.posDiscount) || 0,
        reference: this.posReference,
        notes: this.posNotes
      });
      this.store.commit(next);
      this.posModal = false;
      this.notify(`¡Venta directa registrada! Pedido ${order.number} agregado y materiales descontados.`);
      this.open(order);
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  // ==========================================
  // PASE DE LISTA / ASISTENCIA DE TRABAJADORES
  // ==========================================
  attendanceDate = dayKey();
  attendanceFilter = 'all';

  get attendanceUsers(): User[] {
    return this.state().users || [];
  }

  get dayAttendances() {
    const records = this.state().attendances || [];
    return this.attendanceUsers.map(user => {
      const record = records.find(r => r.userId === user.id && r.date === this.attendanceDate);
      return {
        user,
        record: record || {
          id: 0,
          userId: user.id,
          userName: user.name,
          userRole: user.roleName,
          date: this.attendanceDate,
          clockIn: undefined,
          clockOut: undefined,
          status: 'Sin registrar',
          notes: ''
        }
      };
    });
  }

  get attendanceStats() {
    const list = this.dayAttendances.map(a => a.record);
    return {
      total: list.length,
      present: list.filter(r => r.status === 'Presente').length,
      late: list.filter(r => r.status === 'Retardo').length,
      absent: list.filter(r => r.status === 'Falta').length,
      justified: list.filter(r => r.status === 'Justificado' || r.status === 'Permiso').length,
      pending: list.filter(r => r.status === 'Sin registrar').length
    };
  }

  quickMark(userId: number, status: string, notes?: string) {
    const nowTime = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false });
    const user = this.state().users?.find(u => u.id === userId);
    const existing = this.state().attendances?.find(a => a.userId === userId && a.date === this.attendanceDate);

    const next = markAttendance(this.state(), {
      userId,
      date: this.attendanceDate,
      clockIn: existing?.clockIn || (status === 'Presente' || status === 'Retardo' ? nowTime : undefined),
      clockOut: existing?.clockOut,
      status,
      notes: notes ?? (existing?.notes || (status === 'Presente' ? 'Entrada confirmada' : status))
    });
    this.store.commit(next);
    this.notify(`Asistencia de ${user?.name || 'colaborador'}: ${status}`);
  }

  quickClockIn(userId: number) {
    const nowTime = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false });
    this.quickMark(userId, 'Presente', `Entrada: ${nowTime}`);
  }

  quickClockOut(userId: number) {
    const nowTime = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false });
    const existing = this.state().attendances?.find(a => a.userId === userId && a.date === this.attendanceDate);
    const next = markAttendance(this.state(), {
      userId,
      date: this.attendanceDate,
      clockIn: existing?.clockIn || '08:00',
      clockOut: nowTime,
      status: existing?.status || 'Presente',
      notes: (existing?.notes ? existing.notes + ' · ' : '') + `Salida: ${nowTime}`
    });
    this.store.commit(next);
    this.notify(`Salida registrada a las ${nowTime}`);
  }

  // ==========================================
  // CRUD DE USUARIOS Y ROLES
  // ==========================================
  userModal = false;
  userForm = { id: 0, name: '', email: '', phone: '', roleId: 2, isActive: true };
  userSearch = '';
  roleModal = false;
  roleForm = { id: 0, name: '', description: '' };

  get filteredUsers(): User[] {
    const list = this.state().users || [];
    if (!this.userSearch.trim()) return list;
    return list.filter(u => this.search(`${u.name} ${u.email} ${u.phone} ${u.roleName}`, this.userSearch));
  }

  get roles(): Role[] {
    return this.state().roles || [];
  }

  openUserModal(u?: User) {
    if (u) {
      this.userForm = { id: u.id, name: u.name, email: u.email, phone: u.phone, roleId: u.roleId, isActive: u.isActive };
    } else {
      this.userForm = { id: 0, name: '', email: '', phone: '', roleId: this.roles[0]?.id || 2, isActive: true };
    }
    this.error.set('');
    this.userModal = true;
  }

  saveUserSubmit() {
    try {
      if (!this.userForm.name.trim() || !this.userForm.email.trim()) {
        throw new Error('Completa el nombre y correo electrónico del colaborador.');
      }
      const next = saveUser(this.state(), this.userForm);
      this.store.commit(next);
      this.userModal = false;
      this.notify(`Colaborador ${this.userForm.name} guardado con éxito.`);
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  toggleUser(id: number) {
    const next = toggleUserStatus(this.state(), id);
    this.store.commit(next);
    this.notify('Estado del colaborador actualizado.');
  }

  deleteUserClick(id: number) {
    if (!confirm('¿Eliminar este colaborador de la plantilla?')) return;
    const next = deleteUser(this.state(), id);
    this.store.commit(next);
    this.notify('Colaborador eliminado.');
  }

  openRoleModal(r?: Role) {
    if (r) {
      this.roleForm = { id: r.id, name: r.name, description: r.description };
    } else {
      this.roleForm = { id: 0, name: '', description: '' };
    }
    this.error.set('');
    this.roleModal = true;
  }

  saveRoleSubmit() {
    try {
      if (!this.roleForm.name.trim()) throw new Error('Escribe el nombre del rol.');
      const next = saveRole(this.state(), { id: this.roleForm.id, name: this.roleForm.name, description: this.roleForm.description, permissions: ['*'] });
      this.store.commit(next);
      this.roleModal = false;
      this.notify('Rol actualizado correctamente.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  // ==========================================
  // EXPORTACIONES A EXCEL (.xlsx / XML)
  // ==========================================
  async exportOrdersExcel() {
    const downloaded = await this.apiService.downloadExcelFromBackend('orders', `Floreria_Pedidos_${dayKey()}.xlsx`);
    if (!downloaded) {
      const headers = ['Pedido', 'Origen / Canal', 'Fecha Entrega', 'Hora', 'Cliente', 'Teléfono', 'Destinatario', 'Estado', 'Total COP', 'Abonos COP', 'Saldo COP'];
      const rows = this.filteredOrders.map(o => [
        o.number,
        o.isDirectSale ? 'Venta en Tienda' : o.deliveryMethod,
        o.deliveryDate,
        o.time,
        o.customer,
        o.phone,
        o.recipient,
        this.label(o.status),
        total(o),
        paid(o),
        balance(o)
      ]);
      this.apiService.downloadExcelLocal([{ name: 'Pedidos y Ventas', headers, rows }], `Floreria_Pedidos_${dayKey()}`);
    }
    this.notify('Excel de pedidos descargado exitosamente.');
  }

  async exportAttendanceExcel() {
    const downloaded = await this.apiService.downloadExcelFromBackend('attendance', `Floreria_Asistencia_${dayKey()}.xlsx`);
    if (!downloaded) {
      const headers = ['Fecha', 'Trabajador', 'Cargo', 'Hora Entrada', 'Hora Salida', 'Estado Asistencia', 'Observaciones'];
      const rows = (this.state().attendances || []).map(a => [
        a.date,
        a.userName,
        a.userRole,
        a.clockIn || '--:--',
        a.clockOut || '--:--',
        a.status,
        a.notes || ''
      ]);
      this.apiService.downloadExcelLocal([{ name: 'Pase de Lista', headers, rows }], `Floreria_Asistencia_${dayKey()}`);
    }
    this.notify('Excel de asistencia y pase de lista descargado exitosamente.');
  }

  async exportInventoryExcel() {
    const downloaded = await this.apiService.downloadExcelFromBackend('inventory', `Floreria_Inventario_${dayKey()}.xlsx`);
    if (!downloaded) {
      const headers = ['Código', 'Material / Flor', 'Categoría', 'Unidad', 'Stock Físico', 'Reservado', 'Disponible', 'Mínimo Alerta', 'Costo Unitario', 'Valor Total Stock', 'Proveedor'];
      const rows = this.state().materials.map(m => [
        m.id,
        m.name,
        m.category,
        m.unit,
        m.stock,
        reserved(this.state(), m.id),
        available(this.state(), m.id),
        m.minimum,
        m.cost,
        m.stock * m.cost,
        m.supplier
      ]);
      this.apiService.downloadExcelLocal([{ name: 'Inventario Floral', headers, rows }], `Floreria_Inventario_${dayKey()}`);
    }
    this.notify('Excel de inventario descargado exitosamente.');
  }

  async exportFinancialExcel() {
    const downloaded = await this.apiService.downloadExcelFromBackend('financial', `Floreria_Financiero_${dayKey()}.xlsx`);
    if (!downloaded) {
      const r = this.report;
      const headers = ['Concepto', 'Valor COP'];
      const rows = [
        ['Ventas entregadas (Ingresos)', r.revenue],
        ['Costo de ventas (Materiales)', r.cost],
        ['Gastos operativos', r.expenses],
        ['Merma y deterioro', r.waste],
        ['Costo cancelaciones', r.canceledCost],
        ['Utilidad neta estimada', r.profit],
        ['Cobros recibidos en caja', r.collected]
      ];
      this.apiService.downloadExcelLocal([{ name: 'Estado Financiero', headers, rows }], `Floreria_Financiero_${dayKey()}`);
    }
    this.notify('Excel financiero descargado exitosamente.');
  }

  // ==========================================
  // MÉTODOS BASE DEL NEGOCIO
  // ==========================================
  constructor() {
    this.onHash();
  }

  @HostListener('window:hashchange')
  onHash() {
    const hash = location.hash.slice(1) as View;
    if (this.navItems.some(n => n.id === hash)) this.view.set(hash);
  }

  go(v: View) {
    this.view.set(v);
    location.hash = v;
    this.menuOpen = false;
    this.error.set('');
    window.scrollTo({ top: 0, behavior: 'instant' });
  }

  get pageTitle() {
    return this.navItems.find(n => n.id === this.view())?.label || 'Inicio';
  }

  get todayLabel() {
    return new Date().toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long' });
  }

  money(n: number) {
    return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(n || 0);
  }

  number(n: number) {
    return new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 }).format(n);
  }

  shortDate(s: string) {
    return new Date(s.length === 10 ? s + 'T12:00:00' : s).toLocaleDateString('es-CO', { day: 'numeric', month: 'short' });
  }

  dateTime(s: string) {
    return new Date(s).toLocaleString('es-CO', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  label(s: OrderStatus) {
    return STATUSES.find(x => x.value === s)?.label || s;
  }

  color(s: OrderStatus) {
    return STATUSES.find(x => x.value === s)?.color || 'neutral';
  }

  initials(name: string) {
    return name.split(' ').slice(0, 2).map(n => n[0]).join('');
  }

  image(o: Order) {
    return this.state().products.find(p => p.id === o.items[0]?.productId)?.image || 'assets/blancas.svg';
  }

  search(text: string, query: string) {
    return text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().includes(query.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase());
  }

  get filteredOrders() {
    return this.state().orders.filter(o => this.search([o.number, o.customer, o.phone, o.recipient, ...o.items.map(i => i.name)].join(' '), this.query))
      .filter(o => !this.statusFilter || o.status === this.statusFilter)
      .filter(o => !this.pendingOnly || balance(o) > 0 && o.status !== 'cancelado')
      .filter(o => this.dateFilter === 'all' || (this.dateFilter === 'today' && o.deliveryDate === this.today) || (this.dateFilter === 'tomorrow' && o.deliveryDate === offsetDay(1)) || (this.dateFilter === 'late' && o.deliveryDate < this.today && !['entregado', 'cancelado'].includes(o.status)));
  }

  open(o: Order) {
    this.selectedId.set(o.id);
    this.detailTab = 'resumen';
    this.error.set('');
    this.detailAmount = balance(o);
    this.receipt = o.recipient;
    this.deliveryNote = '';
    this.detailReference = '';
  }

  closeDetail() {
    this.selectedId.set('');
    this.error.set('');
  }

  newOrder(p?: Product) {
    const d = blankOrder(this.state());
    if (p) d.items = [newLine(p, this.state().materials)];
    this.editing = false;
    this.closeDetail();
    this.draft.set(d);
  }

  edit(o: Order) {
    this.editing = true;
    this.closeDetail();
    this.draft.set(structuredClone(o));
  }

  saveDraft(o: Order) {
    try {
      this.store.commit(saveOrder(this.state(), o));
      this.draft.set(null);
      this.open(this.state().orders.find(x => x.id === o.id)!);
      this.notify('Pedido guardado correctamente.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  change(o: Order, status: OrderStatus) {
    if (status === 'cancelado' && !confirm('¿Cancelar ' + o.number + '? Se liberan reservas; los materiales consumidos no se recuperan.')) return;
    try {
      this.store.commit(transition(this.state(), o.id, status, this.receipt, this.deliveryNote));
      this.error.set('');
      this.notify('Pedido actualizado: ' + this.label(status));
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  pay(o: Order) {
    try {
      this.store.commit(addPayment(this.state(), o.id, Number(this.detailAmount), this.detailMethod, this.detailReference));
      this.detailAmount = balance(this.state().orders.find(x => x.id === o.id)!);
      this.error.set('');
      this.notify('Abono registrado con éxito.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  notify(text: string) {
    this.toast.set(text);
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(''), 5000);
  }

  get bars() {
    const amounts = Array.from({ length: 7 }, (_, i) => ({ day: offsetDay(i - 6), value: summary(this.state(), offsetDay(i - 6), offsetDay(i - 6)).revenue }));
    const max = Math.max(...amounts.map(b => b.value), 1);
    return amounts.map(b => ({ ...b, height: Math.max(3, b.value / max * 100), label: new Date(b.day + 'T12:00:00').toLocaleDateString('es-CO', { weekday: 'short' }) }));
  }

  get calendarLabel() {
    return this.calendarMonth.toLocaleDateString('es-CO', { month: 'long', year: 'numeric' });
  }

  get calendarCells() {
    const start = new Date(this.calendarMonth);
    start.setDate(1 - (start.getDay() + 6) % 7);
    return Array.from({ length: 42 }, (_, i) => {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      const key = dayKey(d);
      return { key, day: d.getDate(), current: d.getMonth() === this.calendarMonth.getMonth(), count: this.state().orders.filter(o => o.deliveryDate === key && o.status !== 'cancelado').length };
    });
  }

  shiftMonth(n: number) {
    this.calendarMonth = new Date(this.calendarMonth.getFullYear(), this.calendarMonth.getMonth() + n, 1);
  }

  resetCalendar() {
    this.calendarMonth = new Date(new Date().getFullYear(), new Date().getMonth(), 1);
    this.calendarDay = dayKey();
  }

  get dayOrders() {
    return this.state().orders.filter(o => o.deliveryDate === this.calendarDay && o.status !== 'cancelado').sort((a, b) => a.time.localeCompare(b.time));
  }

  get filteredProducts() {
    return this.state().products.filter(p => this.search(p.name, this.catalogQuery) && (!this.catalogCategory || p.category === this.catalogCategory));
  }

  productCost(p: Product) {
    return p.labor + p.recipe.reduce((n, r) => n + r.quantity * (this.state().materials.find(m => m.id === r.materialId)?.cost || 0), 0);
  }

  available(m: Material) {
    return available(this.state(), m.id);
  }

  reserved(m: Material) {
    return reserved(this.state(), m.id);
  }

  materialName(id: string) {
    return this.state().materials.find(m => m.id === id)?.name || id;
  }

  get filteredMaterials() {
    return this.state().materials.filter(m => this.search(m.name, this.stockQuery) && (!this.stockCategory || m.category === this.stockCategory) && (!this.onlyLow || available(this.state(), m.id) <= m.minimum));
  }

  get inventoryValue() {
    return this.state().materials.reduce((n, m) => n + m.stock * m.cost, 0);
  }

  openStock(type: 'entrada' | 'merma', id = 'rosa') {
    const m = this.state().materials.find(m => m.id === id)!;
    this.movement = { materialId: id, type, quantity: 1, cost: m.cost, reason: '' };
    this.error.set('');
    this.stockModal = true;
  }

  selectStockMaterial() {
    this.movement.cost = this.state().materials.find(m => m.id === this.movement.materialId)?.cost || 0;
  }

  saveStock() {
    try {
      this.store.commit(moveStock(this.state(), this.movement.materialId, this.movement.type, Number(this.movement.quantity), this.movement.reason, Number(this.movement.cost)));
      this.stockModal = false;
      this.notify('Movimiento de inventario registrado.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  get paymentOrders() {
    return this.state().orders.filter(o => o.status !== 'cancelado' && (!this.onlyBalance || balance(o) > 0) && this.search(o.customer + ' ' + o.number, this.paymentQuery));
  }

  get allPayments() {
    return this.state().orders.flatMap(o => o.payments.map(p => ({ ...p, order: o.number, customer: o.customer }))).sort((a, b) => b.date.localeCompare(a.date));
  }

  openExpense() {
    this.expense = { category: 'Domicilios', description: '', amount: 0, date: dayKey(), method: 'Efectivo' };
    this.error.set('');
    this.expenseModal = true;
  }

  saveExpense() {
    try {
      const e = this.expense;
      if (!e.description.trim() || !e.date || !Number.isFinite(Number(e.amount)) || Number(e.amount) <= 0) {
        throw new Error('Completa la descripción, fecha y un valor mayor que cero.');
      }
      this.store.commit({ ...this.state(), expenses: [{ ...e, id: uid(), amount: Math.round(Number(e.amount)) }, ...this.state().expenses] });
      this.expenseModal = false;
      this.notify('Gasto registrado.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  get filteredExpenses() {
    return this.state().expenses.filter(e => e.date >= this.expenseFrom && e.date <= this.expenseTo).sort((a, b) => b.date.localeCompare(a.date));
  }

  get expenseTotal() {
    return this.filteredExpenses.reduce((n, e) => n + e.amount, 0);
  }

  get clients() {
    const map = new Map<string, { name: string; phone: string; email: string; orders: number; spent: number; balance: number }>();
    for (const o of this.state().orders) {
      let c = map.get(o.phone);
      if (!c) {
        c = { name: o.customer, phone: o.phone, email: o.email, orders: 0, spent: 0, balance: 0 };
        map.set(o.phone, c);
      }
      c.orders++;
      if (o.status === 'entregado') c.spent += total(o);
      if (o.status !== 'cancelado') c.balance += balance(o);
    }
    return Array.from(map.values()).filter(c => this.search(c.name + ' ' + c.phone, this.contactQuery));
  }

  clientOrders(phone: string) {
    this.query = phone;
    this.dateFilter = 'all';
    this.statusFilter = '';
    this.pendingOnly = false;
    this.go('pedidos');
  }

  get suppliers() {
    return Array.from(new Set(this.state().materials.map(m => m.supplier)))
      .filter(s => this.search(s, this.contactQuery))
      .map(name => ({
        name,
        materials: this.state().materials.filter(m => m.supplier === name),
        purchases: this.state().movements.filter(m => m.type === 'entrada' && this.state().materials.find(x => x.id === m.materialId)?.supplier === name).reduce((n, m) => n + m.quantity * m.cost, 0)
      }));
  }

  get report() {
    return summary(this.state(), this.reportFrom, this.reportTo);
  }

  get rankings() {
    const rows = new Map<string, { name: string; quantity: number; revenue: number; cost: number }>();
    for (const o of this.state().orders) {
      if (o.status !== 'entregado' || !o.deliveredAt) continue;
      const d = dayKey(new Date(o.deliveredAt));
      if (d < this.reportFrom || d > this.reportTo) continue;
      for (const i of o.items) {
        let p = rows.get(i.productId);
        if (!p) {
          p = { name: i.name, quantity: 0, revenue: 0, cost: 0 };
          rows.set(i.productId, p);
        }
        p.quantity += i.quantity;
        p.revenue += i.price * i.quantity;
        p.cost += (i.labor + i.recipe.reduce((n, r) => n + r.quantity * r.unitCost, 0)) * i.quantity;
      }
    }
    return Array.from(rows.values()).sort((a, b) => b.quantity - a.quantity);
  }

  get expenseGroups() {
    return this.expenseCategories.map(name => ({
      name,
      value: this.state().expenses.filter(e => e.category === name && e.date >= this.reportFrom && e.date <= this.reportTo).reduce((n, e) => n + e.amount, 0)
    })).filter(g => g.value > 0);
  }

  range(days: number) {
    this.reportFrom = offsetDay(-days + 1);
    this.reportTo = dayKey();
  }

  download(name: string, content: string, type: string) {
    const url = URL.createObjectURL(new Blob([content], { type }));
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  exportOrders() {
    const rows = [['Pedido', 'Cliente', 'Destinatario', 'Entrega', 'Hora', 'Estado', 'Total COP', 'Abonos COP', 'Saldo COP'], ...this.filteredOrders.map(o => [o.number, o.customer, o.recipient, o.deliveryDate, o.time, this.label(o.status), total(o), paid(o), balance(o)])];
    this.download('flore-pedidos.csv', '\uFEFF' + rows.map(row => row.map(csvCell).join(';')).join('\r\n'), 'text/csv;charset=utf-8');
  }

  exportReport() {
    const r = this.report;
    const rows = [['Concepto', 'Valor COP'], ['Ventas entregadas', r.revenue], ['Costo de ventas', r.cost], ['Gastos operativos', r.expenses], ['Merma', r.waste], ['Costo cancelaciones', r.canceledCost], ['Utilidad estimada', r.profit], ['Cobros recibidos', r.collected]];
    this.download('flore-informe-' + this.reportFrom + '.csv', '\uFEFF' + rows.map(row => row.map(csvCell).join(';')).join('\r\n'), 'text/csv;charset=utf-8');
  }

  exportData() {
    this.download('flore-demo-' + this.today + '.json', JSON.stringify({ exportedAt: new Date().toISOString(), note: 'Demo local; NO incluye fotografías.', data: this.state() }, null, 2), 'application/json');
    this.notify('Registros exportados.');
  }

  print() {
    window.print();
  }

  async reset() {
    if (!confirm('¿Reiniciar la demo? Se restaurarán los datos originales ficticios.')) return;
    try {
      await clearPhotos();
      this.store.reset();
      this.notify('Demo reiniciada con datos predeterminados.');
    } catch (e) {
      this.notify((e as Error).message);
    }
  }

  saveBusiness(name: string) {
    if (!name.trim()) {
      this.notify('Escribe un nombre para la floristería.');
      return;
    }
    try {
      this.store.commit({ ...this.state(), business: name.trim().slice(0, 80) });
      this.notify('Nombre actualizado en la floristería.');
    } catch (e) {
      this.notify((e as Error).message);
    }
  }
}
