import { Component, computed, HostListener, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IconComponent } from './icon.component';
import { DialogComponent } from './dialog.component';
import { PhotosComponent, clearPhotos } from './photos.component';
import { OrderFormComponent } from './order-form.component';
import { AppStore } from './store';
import { ApiService, SyncStatus } from './api.service';
import {
  type Order, type OrderStatus, type Product, type Material, type User, type Role, type Attendance,
  STATUSES, METHODS, total, paid, balance, orderCost, paymentLabel, nextStatus,
  reserved, available, dayKey, offsetDay, blankOrder, newLine, saveOrder, addPayment,
  transition, moveStock, summary, csvCell, uid, directSale, markAttendance, saveUser,
  toggleUserStatus, deleteUser, saveRole, validateOrder
} from './domain';

type View = 'inicio' | 'pedidos' | 'agenda' | 'catalogo' | 'inventario' | 'asistencia' | 'usuarios' | 'pagos' | 'gastos' | 'contactos' | 'informes' | 'ajustes';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, IconComponent, DialogComponent, PhotosComponent, OrderFormComponent],
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit, OnDestroy {
  readonly store = inject(AppStore);
  readonly apiService = inject(ApiService);
  readonly state = this.store.state;

  // ==========================================
  // AUTENTICACIÓN Y CONTROL DE ACCESO
  // ==========================================
  readonly isLoggedIn = signal<boolean>(this.apiService.isLoggedIn);
  loginEmail = 'admin@floristeria.com';
  loginPassword = 'admin123';
  readonly loginLoading = signal<boolean>(false);
  readonly loginError = signal<string>('');
  readonly currentUser = computed(() => this.apiService.user);

  get userInitials(): string {
    const name = this.currentUser()?.name || 'Administrador';
    return name.split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
  }

  // ==========================================
  // SINCRONIZACIÓN CON TIENDA WEB
  // ==========================================
  readonly syncingWeb = signal<boolean>(false);
  readonly webSyncStatus = signal<SyncStatus | null>(null);

  readonly navItems: { id: View; label: string; icon: string; group: string }[] = [
    { id: 'inicio', label: 'Inicio', icon: 'home', group: 'FLORISTERÍA' },
    { id: 'pedidos', label: 'Pedidos', icon: 'orders', group: '' },
    { id: 'agenda', label: 'Agenda de entregas', icon: 'calendar', group: '' },
    { id: 'catalogo', label: 'Catálogo floral', icon: 'flower', group: '' },
    { id: 'inventario', label: 'Inventario y merma', icon: 'stock', group: '' },
    { id: 'asistencia', label: 'Pase de lista', icon: 'clipboard', group: 'EQUIPO Y GESTIÓN' },
    { id: 'usuarios', label: 'Usuarios y roles', icon: 'users', group: '' },
    { id: 'pagos', label: 'Ventas y cobros', icon: 'wallet', group: 'ADMINISTRACIÓN' },
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
  // VENTA DIRECTA EN LOCAL (POS)
  // ==========================================
  posModal = false;
  posCustomer = 'Cliente Mostrador';
  posPhone = '';
  posPaymentMethod = 'Efectivo';
  posDiscount = 0;
  posReference = 'Venta física mostrador';
  posNotes = '';
  posSelectedProductId = '';
  posItems: { product: Product; quantity: number; price: number }[] = [];

  // ==========================================
  // PASE DE LISTA
  // ==========================================
  attendanceDate = dayKey();
  attendanceFilter = 'all';

  // ==========================================
  // MODALES DE USUARIOS Y ROLES
  // ==========================================
  userModal = false;
  userSearch = '';
  userForm = { id: 0, name: '', email: '', phone: '', roleId: 1, isActive: true };
  roleModal = false;
  roleForm = { id: 0, name: '', description: '' };

  // ==========================================
  // MODALES DE PRODUCTOS Y RECETAS
  // ==========================================
  productModal = false;
  productForm = {
    id: '',
    name: '',
    category: 'Ramos',
    price: 150000,
    labor: 15000,
    image: '',
    description: '',
    recipe: [] as { materialId: string; quantity: number; unitCost: number }[]
  };
  productRecipeMaterialId = '';
  productRecipeQty = 1;
  uploadingProductImage = signal(false);
  private autoSyncInterval: any = null;

  ngOnInit() {
    if (this.isLoggedIn()) {
      this.store.refreshFromDatabase();
      this.checkWebSyncStatus();
    }
    this.startAutoSync();
  }

  startAutoSync() {
    if (this.autoSyncInterval) clearInterval(this.autoSyncInterval);
    this.autoSyncInterval = setInterval(() => {
      if (this.isLoggedIn()) {
        this.store.refreshFromDatabase();
      }
    }, 15000);
  }

  ngOnDestroy() {
    if (this.autoSyncInterval) clearInterval(this.autoSyncInterval);
  }

  async doLogin() {
    this.loginLoading.set(true);
    this.loginError.set('');
    try {
      await this.apiService.login(this.loginEmail, this.loginPassword);
      this.isLoggedIn.set(true);
      await this.store.refreshFromDatabase();
      this.checkWebSyncStatus();
      this.notify(`¡Bienvenido a Florería La Carreta, ${this.currentUser()?.name || ''}!`);
    } catch (err) {
      this.loginError.set((err as Error).message);
    } finally {
      this.loginLoading.set(false);
    }
  }

  quickLogin(email: string, pass: string) {
    this.loginEmail = email;
    this.loginPassword = pass;
    this.doLogin();
  }

  doLogout() {
    this.apiService.clearSession();
    this.isLoggedIn.set(false);
    this.notify('Sesión cerrada correctamente.');
  }

  async checkWebSyncStatus() {
    try {
      const st = await this.apiService.getSyncStatus();
      this.webSyncStatus.set(st);
    } catch { }
  }

  async syncWebShop() {
    this.syncingWeb.set(true);
    try {
      const res = await this.apiService.syncWeb();
      await this.store.refreshFromDatabase();
      await this.checkWebSyncStatus();
      this.notify(res.message || 'Sincronización con tienda web completada.');
    } catch (e) {
      this.notify('Error al sincronizar con tienda web: ' + (e as Error).message);
    } finally {
      this.syncingWeb.set(false);
    }
  }

  // --- VENTA EN LOCAL ---
  openPos() {
    this.posModal = true;
    this.posCustomer = 'Cliente Mostrador';
    this.posPhone = '';
    this.posPaymentMethod = 'Efectivo';
    this.posDiscount = 0;
    this.posReference = 'Venta física mostrador';
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

  async confirmPosSale() {
    try {
      if (!this.posItems.length) throw new Error('Agrega al menos un arreglo o producto a la venta.');
      this.error.set('');
      const saleDto = {
        customer: this.posCustomer,
        phone: this.posPhone,
        items: this.posItems.map(i => ({
          productId: i.product.id,
          name: i.product.name,
          quantity: i.quantity,
          price: i.price
        })),
        paymentMethod: this.posPaymentMethod,
        discount: Number(this.posDiscount) || 0,
        reference: this.posReference,
        notes: this.posNotes
      };
      const order = await this.apiService.createDirectSale(saleDto);
      await this.store.refreshFromDatabase();
      this.posModal = false;
      this.notify(`¡Venta directa registrada! Pedido ${order.number} guardado en base de datos.`);
      const created = this.state().orders.find(x => x.number === order.number || x.id === order.id);
      if (created) this.open(created);
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  // --- PASE DE LISTA ---
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
          status: 'Pendiente',
          notes: ''
        }
      };
    });
  }

  get attendanceStats() {
    const records = this.dayAttendances;
    return {
      total: records.length,
      present: records.filter(r => r.record.status === 'Presente').length,
      late: records.filter(r => r.record.status === 'Retardo').length,
      absent: records.filter(r => r.record.status === 'Falta').length,
      justified: records.filter(r => r.record.status === 'Justificado' || r.record.status === 'Permiso').length,
      pending: records.filter(r => r.record.status === 'Pendiente').length
    };
  }

  async quickClockIn(userId: number) {
    const time = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false });
    await this.markAttendanceRecord(userId, 'Presente', undefined, time, undefined);
  }

  async quickClockOut(userId: number) {
    const time = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false });
    await this.markAttendanceRecord(userId, 'Presente', undefined, undefined, time);
  }

  async quickMark(userId: number, status: string) {
    await this.markAttendanceRecord(userId, status);
  }

  async markAttendanceRecord(userId: number, status: string, notes?: string, clockIn?: string, clockOut?: string) {
    try {
      const existing = this.dayAttendances.find(a => a.user.id === userId)?.record;
      await this.apiService.markAttendance({
        userId,
        date: this.attendanceDate,
        status,
        notes: notes ?? existing?.notes,
        clockIn: clockIn ?? existing?.clockIn,
        clockOut: clockOut ?? existing?.clockOut
      });
      await this.store.refreshFromDatabase();
      this.notify(`Asistencia guardada en base de datos.`);
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  // --- USUARIOS Y ROLES ---
  get users(): User[] {
    return this.state().users || [];
  }

  get filteredUsers(): User[] {
    return this.users.filter(u => !this.userSearch || this.search(u.name + ' ' + u.email + ' ' + (u.phone || ''), this.userSearch));
  }

  get roles(): Role[] {
    return this.state().roles || [];
  }

  openUserModal(u?: User) {
    if (u) {
      this.userForm = { id: u.id, name: u.name, email: u.email, phone: u.phone, roleId: u.roleId, isActive: u.isActive };
    } else {
      this.userForm = { id: 0, name: '', email: '', phone: '', roleId: this.roles[0]?.id || 1, isActive: true };
    }
    this.error.set('');
    this.userModal = true;
  }

  async saveUserSubmit() {
    try {
      if (!this.userForm.name.trim()) throw new Error('El nombre es obligatorio.');
      if (!this.userForm.email.trim()) throw new Error('El correo electrónico es obligatorio.');
      if (this.userForm.id) {
        await this.apiService.updateUser(this.userForm.id, this.userForm);
      } else {
        await this.apiService.createUser({ ...this.userForm, password: 'florer123' });
      }
      await this.store.refreshFromDatabase();
      this.userModal = false;
      this.notify(`Colaborador ${this.userForm.name} guardado en base de datos.`);
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  async toggleUser(id: number) {
    await this.apiService.toggleUserStatus(id);
    await this.store.refreshFromDatabase();
    this.notify('Estado del colaborador actualizado en base de datos.');
  }

  async deleteUserClick(id: number) {
    if (!confirm('¿Eliminar este colaborador de la plantilla?')) return;
    await this.apiService.deleteUser(id);
    await this.store.refreshFromDatabase();
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

  async saveRoleSubmit() {
    try {
      if (!this.roleForm.name.trim()) throw new Error('Escribe el nombre del rol.');
      if (this.roleForm.id) {
        await this.apiService.updateRole(this.roleForm.id, this.roleForm);
      } else {
        await this.apiService.createRole({ ...this.roleForm, permissionsJson: '["*"]' });
      }
      await this.store.refreshFromDatabase();
      this.roleModal = false;
      this.notify('Rol guardado en base de datos.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  // --- GESTIÓN DE PRODUCTOS Y RECETAS ---
  openProductModal(p?: Product) {
    if (p) {
      this.productForm = {
        id: p.id,
        name: p.name,
        category: p.category || 'Ramos',
        price: p.price,
        labor: p.labor || 0,
        image: p.image || '',
        description: p.description || '',
        recipe: (p.recipe || []).map(r => ({ ...r }))
      };
    } else {
      this.productForm = {
        id: '',
        name: '',
        category: 'Ramos',
        price: 150000,
        labor: 15000,
        image: 'https://floreria.molinazdev.lat/images/romantic-roses.jpg',
        description: '',
        recipe: []
      };
    }
    if (this.state().materials.length && !this.productRecipeMaterialId) {
      this.productRecipeMaterialId = this.state().materials[0].id;
    }
    this.error.set('');
    this.productModal = true;
  }

  addProductRecipeItem() {
    if (!this.productRecipeMaterialId) return;
    const mat = this.state().materials.find(m => m.id === this.productRecipeMaterialId);
    if (!mat) return;
    const existing = this.productForm.recipe.find(r => r.materialId === mat.id);
    if (existing) {
      existing.quantity += Number(this.productRecipeQty) || 1;
    } else {
      this.productForm.recipe.push({
        materialId: mat.id,
        quantity: Number(this.productRecipeQty) || 1,
        unitCost: mat.cost
      });
    }
  }

  removeProductRecipeItem(idx: number) {
    this.productForm.recipe.splice(idx, 1);
  }

  get productFormCost(): number {
    return (Number(this.productForm.labor) || 0) +
      this.productForm.recipe.reduce((acc, r) => acc + (r.quantity * (r.unitCost || 0)), 0);
  }

  async saveProductSubmit() {
    try {
      if (!this.productForm.name.trim()) throw new Error('El nombre del producto es obligatorio.');
      if (!this.productForm.price || this.productForm.price <= 0) throw new Error('El precio debe ser mayor que cero.');

      if (this.productForm.id) {
        await this.apiService.updateProduct(this.productForm.id, this.productForm);
        this.notify(`Producto "${this.productForm.name}" actualizado y sincronizado en la tienda web.`);
      } else {
        await this.apiService.createProduct(this.productForm);
        this.notify(`Producto "${this.productForm.name}" creado y publicado en la tienda web.`);
      }
      await this.store.refreshFromDatabase();
      this.productModal = false;
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  async deleteProductClick(p: Product) {
    if (!confirm(`¿Estás seguro de eliminar "${p.name}" del catálogo y de la tienda web?`)) return;
    try {
      await this.apiService.deleteProduct(p.id);
      await this.store.refreshFromDatabase();
      this.notify(`Producto "${p.name}" eliminado del catálogo y de la web.`);
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  async onProductImageSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || !input.files.length) return;
    const file = input.files[0];

    if (file.size > 10 * 1024 * 1024) {
      this.notify('La imagen excede el límite de 10 MB.');
      return;
    }

    this.uploadingProductImage.set(true);
    try {
      const res = await this.apiService.uploadImage(file);
      this.productForm.image = res.url;
      this.notify('Fotografía subida con éxito y alojada para la tienda web.');
    } catch (e) {
      this.notify('Error al subir la imagen: ' + (e as Error).message);
    } finally {
      this.uploadingProductImage.set(false);
      input.value = '';
    }
  }

  removeProductImage() {
    this.productForm.image = '';
  }

  // --- EXPORTACIONES EXCEL ---
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
      this.apiService.downloadExcelLocal([{ name: 'Inventario', headers, rows }], `Floreria_Inventario_${dayKey()}`);
    }
    this.notify('Excel de inventario descargado exitosamente.');
  }

  async exportFinancialExcel() {
    const downloaded = await this.apiService.downloadExcelFromBackend('financial', `Floreria_Financiero_${dayKey()}.xlsx`);
    if (!downloaded) {
      const headers = ['Concepto', 'Valor COP'];
      const rows = [
        ['Ventas Entregadas', this.report.revenue],
        ['Costo de Arreglos y Flores', this.report.cost],
        ['Gastos Operativos', this.report.expenses],
        ['Pérdida por Merma', this.report.waste],
        ['Utilidad Operativa Estimada', this.report.profit],
        ['Total Cobros Recibidos', this.report.collected]
      ];
      this.apiService.downloadExcelLocal([{ name: 'Rentabilidad', headers, rows }], `Floreria_Financiero_${dayKey()}`);
    }
    this.notify('Informe financiero exportado a Excel.');
  }

  // --- NAVEGACIÓN Y DETALLES DE PEDIDO ---
  go(view: View) {
    this.view.set(view);
    this.menuOpen = false;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  get pageTitle() {
    return this.navItems.find(n => n.id === this.view())?.label ?? 'Inicio';
  }

  open(o: Order) {
    this.selectedId.set(o.id);
    this.editing = false;
    this.detailTab = 'resumen';
    this.error.set('');
    this.detailAmount = balance(o);
    this.detailMethod = 'Nequi';
    this.detailReference = '';
    this.receipt = '';
    this.deliveryNote = '';
  }

  closeDetail() {
    this.selectedId.set('');
  }

  newOrder(product?: Product) {
    this.editing = false;
    const d = blankOrder(this.state());
    if (product) {
      d.items = [newLine(product, this.state().materials)];
    }
    this.closeDetail();
    this.draft.set(d);
  }

  edit(o: Order) {
    this.editing = true;
    this.closeDetail();
    this.draft.set(structuredClone(o));
  }

  async saveDraft(o: Order) {
    try {
      validateOrder(o);
      const saved = await this.apiService.createOrder(o);
      await this.store.refreshFromDatabase();
      this.draft.set(null);
      const fresh = this.state().orders.find(x => x.id === saved.id || x.number === saved.number);
      if (fresh) this.open(fresh);
      this.notify('Pedido guardado correctamente en base de datos.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  async change(o: Order, status: OrderStatus) {
    if (status === 'cancelado' && !confirm('¿Cancelar ' + o.number + '? Se liberan reservas de inventario.')) return;
    try {
      await this.apiService.updateOrderStatus(o.id, status, this.deliveryNote, this.receipt);
      await this.store.refreshFromDatabase();
      this.error.set('');
      this.notify('Pedido actualizado: ' + this.label(status));
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  async pay(o: Order) {
    try {
      await this.apiService.addPayment(o.id, {
        amount: Number(this.detailAmount),
        method: this.detailMethod,
        reference: this.detailReference
      });
      await this.store.refreshFromDatabase();
      const fresh = this.state().orders.find(x => x.id === o.id);
      if (fresh) this.detailAmount = balance(fresh);
      this.error.set('');
      this.notify('Abono registrado en base de datos.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  notify(text: string) {
    this.toast.set(text);
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(''), 5000);
  }

  // --- GRÁFICOS Y RESÚMENES ---
  get bars() {
    const amounts = Array.from({ length: 7 }, (_, i) => ({ day: offsetDay(i - 6), value: summary(this.state(), offsetDay(i - 6), offsetDay(i - 6)).revenue }));
    const max = Math.max(...amounts.map(a => a.value), 1);
    const dayNames = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
    return amounts.map(a => {
      const d = new Date(a.day + 'T12:00:00');
      return {
        day: a.day,
        value: a.value,
        height: Math.max(8, Math.round((a.value / max) * 100)),
        label: dayNames[d.getDay()]
      };
    });
  }

  get calendarCells() {
    const y = this.calendarMonth.getFullYear(), m = this.calendarMonth.getMonth();
    const first = new Date(y, m, 1), startDay = (first.getDay() + 6) % 7;
    const daysInMonth = new Date(y, m + 1, 0).getDate();
    const prevDays = new Date(y, m, 0).getDate();
    const cells: { key: string; day: number; current: boolean; count: number }[] = [];

    for (let i = startDay - 1; i >= 0; i--) {
      const prevDate = new Date(y, m - 1, prevDays - i);
      const key = dayKey(prevDate);
      cells.push({ key, day: prevDays - i, current: false, count: this.state().orders.filter(o => o.deliveryDate === key && o.status !== 'cancelado').length });
    }

    for (let i = 1; i <= daysInMonth; i++) {
      const currDate = new Date(y, m, i);
      const key = dayKey(currDate);
      cells.push({ key, day: i, current: true, count: this.state().orders.filter(o => o.deliveryDate === key && o.status !== 'cancelado').length });
    }

    const remaining = 42 - cells.length;
    for (let i = 1; i <= remaining; i++) {
      const nextDate = new Date(y, m + 1, i);
      const key = dayKey(nextDate);
      cells.push({ key, day: i, current: false, count: this.state().orders.filter(o => o.deliveryDate === key && o.status !== 'cancelado').length });
    }

    return cells;
  }

  get dayOrders() {
    return this.state().orders.filter(o => o.deliveryDate === this.calendarDay && o.status !== 'cancelado').sort((a, b) => a.time.localeCompare(b.time));
  }

  shiftMonth(offset: number) {
    this.calendarMonth = new Date(this.calendarMonth.getFullYear(), this.calendarMonth.getMonth() + offset, 1);
  }

  resetCalendar() {
    this.calendarMonth = new Date(new Date().getFullYear(), new Date().getMonth(), 1);
    this.calendarDay = this.today;
  }

  get calendarLabel() {
    return this.calendarMonth.toLocaleDateString('es-CO', { month: 'long', year: 'numeric' });
  }

  get todayLabel() {
    return new Date().toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long' });
  }

  // --- FILTROS DE PEDIDOS ---
  get filteredOrders() {
    return this.state().orders.filter(o => {
      if (this.statusFilter && o.status !== this.statusFilter) return false;
      if (this.pendingOnly && balance(o) === 0) return false;
      if (this.dateFilter === 'today' && o.deliveryDate !== this.today) return false;
      if (this.dateFilter === 'tomorrow' && o.deliveryDate !== offsetDay(1)) return false;
      if (this.dateFilter === 'late' && (o.deliveryDate >= this.today || ['entregado', 'cancelado'].includes(o.status))) return false;
      if (!this.query) return true;
      return this.search(o.customer + ' ' + o.number + ' ' + o.recipient + ' ' + o.phone, this.query);
    });
  }

  // --- CATÁLOGO ---
  get filteredProducts() {
    return this.state().products.filter(p => {
      if (this.catalogCategory && p.category !== this.catalogCategory) return false;
      if (!this.catalogQuery) return true;
      return this.search(p.name + ' ' + p.description, this.catalogQuery);
    });
  }

  // --- INVENTARIO ---
  openStock(type: 'entrada' | 'merma', id = 'rosa') {
    const m = this.state().materials.find(m => m.id === id) || this.state().materials[0];
    const cost = m ? m.cost : 3000;
    this.movement = { materialId: m ? m.id : id, type, quantity: 1, cost, reason: '' };
    this.error.set('');
    this.stockModal = true;
  }

  selectStockMaterial() {
    this.movement.cost = this.state().materials.find(m => m.id === this.movement.materialId)?.cost || 0;
  }

  async saveStock() {
    try {
      await this.apiService.createMovement({
        materialId: this.movement.materialId,
        type: this.movement.type,
        quantity: Number(this.movement.quantity),
        cost: Number(this.movement.cost),
        reason: this.movement.reason
      });
      await this.store.refreshFromDatabase();
      this.stockModal = false;
      this.notify('Movimiento de inventario guardado en base de datos.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  get filteredMaterials() {
    return this.state().materials.filter(m => {
      if (this.stockCategory && m.category !== this.stockCategory) return false;
      if (this.onlyLow && available(this.state(), m.id) > m.minimum) return false;
      if (!this.stockQuery) return true;
      return this.search(m.name + ' ' + m.category, this.stockQuery);
    });
  }

  get inventoryValue() {
    return this.state().materials.reduce((sum, m) => sum + m.stock * m.cost, 0);
  }

  // --- GASTOS ---
  openExpense() {
    this.expense = { category: 'Domicilios', description: '', amount: 0, date: dayKey(), method: 'Efectivo' };
    this.error.set('');
    this.expenseModal = true;
  }

  async saveExpense() {
    try {
      const e = this.expense;
      if (!e.description.trim() || !e.date || !Number.isFinite(Number(e.amount)) || Number(e.amount) <= 0) {
        throw new Error('Completa la descripción, fecha y un valor mayor que cero.');
      }
      await this.apiService.createExpense({
        category: e.category,
        description: e.description,
        amount: Math.round(Number(e.amount)),
        date: e.date,
        method: e.method
      });
      await this.store.refreshFromDatabase();
      this.expenseModal = false;
      this.notify('Gasto registrado en base de datos.');
    } catch (e) {
      this.error.set((e as Error).message);
    }
  }

  get filteredExpenses() {
    return (this.state().expenses || []).filter(e => e.date >= this.expenseFrom && e.date <= this.expenseTo);
  }

  get expenseTotal() {
    return this.filteredExpenses.reduce((sum, e) => sum + e.amount, 0);
  }

  // --- PAGOS Y COBROS ---
  get paymentOrders() {
    return this.state().orders.filter(o => o.status !== 'cancelado' && (!this.onlyBalance || balance(o) > 0) && this.search(o.customer + ' ' + o.number, this.paymentQuery));
  }

  get allPayments() {
    return this.state().orders.flatMap(o => o.payments.map(p => ({ ...p, order: o.number, customer: o.customer }))).sort((a, b) => b.date.localeCompare(a.date));
  }

  // --- INFORMES ---
  get report() {
    return summary(this.state(), this.reportFrom, this.reportTo);
  }

  get rankings() {
    const map = new Map<string, { name: string; quantity: number; revenue: number; cost: number }>();
    this.state().orders.filter(o => o.status === 'entregado' && o.deliveryDate >= this.reportFrom && o.deliveryDate <= this.reportTo).forEach(o => {
      o.items.forEach(i => {
        const row = map.get(i.name) || { name: i.name, quantity: 0, revenue: 0, cost: 0 };
        row.quantity += i.quantity;
        row.revenue += i.price * i.quantity;
        row.cost += (i.labor + i.recipe.reduce((n, r) => n + r.quantity * r.unitCost, 0)) * i.quantity;
        map.set(i.name, row);
      });
    });
    return Array.from(map.values()).sort((a, b) => b.revenue - a.revenue);
  }

  get expenseGroups() {
    const map = new Map<string, number>();
    this.filteredExpenses.forEach(e => map.set(e.category, (map.get(e.category) || 0) + e.amount));
    return Array.from(map.entries()).map(([name, value]) => ({ name, value })).sort((a, b) => b.value - a.value);
  }

  // --- CONTACTOS ---
  get clients() {
    const map = new Map<string, { name: string; phone: string; email: string; orders: number; spent: number; balance: number }>();
    this.state().orders.forEach(o => {
      const c = map.get(o.phone) || { name: o.customer, phone: o.phone, email: o.email, orders: 0, spent: 0, balance: 0 };
      c.orders++;
      if (o.status === 'entregado') c.spent += total(o);
      if (o.status !== 'cancelado') c.balance += balance(o);
      map.set(o.phone, c);
    });
    return Array.from(map.values()).filter(c => this.search(c.name + ' ' + c.phone, this.contactQuery));
  }

  get suppliers() {
    const map = new Map<string, { name: string; materials: Material[]; purchases: number }>();
    this.state().materials.forEach(m => {
      const s = map.get(m.supplier) || { name: m.supplier, materials: [], purchases: 0 };
      s.materials.push(m);
      map.set(m.supplier, s);
    });
    return Array.from(map.values());
  }

  clientOrders(phone: string) {
    this.query = phone;
    this.go('pedidos');
  }

  range(days: number) {
    this.reportFrom = offsetDay(-days + 1);
    this.reportTo = this.today;
  }

  print() {
    window.print();
  }

  exportReport() {
    const csv = ['Concepto,Valor', `Ventas,${this.report.revenue}`, `Costos,${this.report.cost}`, `Gastos,${this.report.expenses}`, `Merma,${this.report.waste}`, `Utilidad,${this.report.profit}`].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `Floreria_Informe_${this.reportFrom}_${this.reportTo}.csv`;
    a.click();
  }

  exportOrders() {
    const csv = ['Pedido,Cliente,Fecha,Estado,Total,Saldo', ...this.filteredOrders.map(o => `${o.number},"${o.customer}",${o.deliveryDate},${o.status},${total(o)},${balance(o)}`)].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `Floreria_Pedidos_${this.today}.csv`;
    a.click();
  }

  // --- HELPERS DE FORMATEO ---
  money(n: number) {
    return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(n || 0);
  }

  number(n: number) {
    return new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 }).format(n || 0);
  }

  shortDate(d?: string) {
    if (!d) return '';
    const parts = d.substring(0, 10).split('-');
    if (parts.length === 3) return `${parts[2]}/${parts[1]}`;
    return d;
  }

  dateTime(iso?: string) {
    if (!iso) return '';
    try {
      const dt = new Date(iso);
      return dt.toLocaleDateString('es-CO', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
    } catch {
      return iso;
    }
  }

  label(st: OrderStatus | string) {
    return this.statuses.find(s => s.value === st)?.label || st;
  }

  color(st: OrderStatus | string) {
    return this.statuses.find(s => s.value === st)?.color || 'neutral';
  }

  image(o: Order) {
    const prod = this.state().products.find(p => p.id === o.items[0]?.productId);
    return prod?.image || 'assets/rosas.svg';
  }

  materialName(id: string) {
    return this.state().materials.find(m => m.id === id)?.name || id;
  }

  productCost(p: Product) {
    return p.labor + p.recipe.reduce((n, r) => n + r.quantity * r.unitCost, 0);
  }

  available(m: Material) {
    return available(this.state(), m.id);
  }

  reserved(m: Material) {
    return reserved(this.state(), m.id);
  }

  saveBusiness(name: string) {
    this.store.state.update(s => ({ ...s, business: name }));
    this.notify('Nombre del negocio actualizado.');
  }

  exportData() {
    const blob = new Blob([JSON.stringify(this.state(), null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `respaldo_floreria_lacarreta_${this.today}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  reset() {
    this.store.refreshFromDatabase();
    this.notify('Datos actualizados desde la base de datos.');
  }

  initials(name: string) {
    return (name || '').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
  }

  search(text: string, term: string) {
    return (text || '').toLowerCase().includes((term || '').toLowerCase().trim());
  }
}
