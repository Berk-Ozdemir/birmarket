import { CommonModule, CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { catchError, forkJoin, of } from 'rxjs';
import {
  CartLine,
  Category,
  CheckoutPayload,
  CheckoutResult,
  CustomerAddress,
  Product,
  StoreApiService,
  StoreOrder,
  StoreUser,
  StorefrontInfo,
} from './store-api.service';

type StoreView = 'shop' | 'orders' | 'admin' | 'order-result';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  storefront: StorefrontInfo | null = null;
  categories: Category[] = [];
  products: Product[] = [];
  cart: CartLine[] = [];
  user: StoreUser | null = null;
  orders: StoreOrder[] = [];
  addresses: CustomerAddress[] = [];
  adminOrders: StoreOrder[] = [];
  adminProducts: Product[] = [];
  adminSummary = { products: 0, lowStock: 0, pendingOrders: 0, revenue: 0 };
  wishlistIds = new Set<number>();
  view: StoreView = 'shop';
  selectedCategoryId: number | null = null;
  searchTerm = '';
  sort = 'featured';
  showCart = false;
  showMobileSearch = false;
  showAuth = false;
  showCheckout = false;
  authMode: 'login' | 'register' | 'forgot' | 'reset' = 'login';
  paymentStage: 'idle' | 'demo' | 'provider' = 'idle';
  selectedProduct: Product | null = null;
  loading = true;
  submitting = false;
  authSubmitting = false;
  errorMessage = '';
  toastMessage = '';
  authError = '';
  demoBadge = true;
  sandboxBadge = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  checkout: CheckoutPayload = this.emptyCheckout();
  registerName = '';
  loginEmail = '';
  loginPassword = '';
  registerEmail = '';
  registerPassword = '';
  resetEmail = '';
  resetToken = '';
  newPassword = '';
  authMessage = '';
  rememberMe = true;
  productDraft = {
    name: '',
    sku: '',
    description: '',
    price: 0,
    compareAtPrice: null as number | null,
    stock: 0,
    categoryId: 0,
    imageUrl: '',
    imageTone: 'clay',
    weightKg: 0.5,
    widthCm: 20,
    lengthCm: 20,
    heightCm: 10,
    isFeatured: false,
    isActive: true,
  };
  categoryDraft = { name: '', description: '', accent: '#c47c5b' };
  couponDraft = {
    code: '',
    percentOff: 10,
    minimumSubtotal: null as number | null,
    expiresAt: null as string | null,
    maximumUses: null as number | null,
    isActive: true,
  };
  couponInput = '';
  couponApplied = '';
  couponPercent = 0;
  checkoutResult: CheckoutResult | null = null;
  paymentFrameUrl: SafeResourceUrl | null = null;
  orderResultMessage = '';
  trackingNumber = '';
  trackingEmail = '';
  selectedAddressId: number | null = null;
  saveAddress = false;
  trackedOrder: StoreOrder | null = null;

  constructor(
    private readonly api: StoreApiService,
    private readonly sanitizer: DomSanitizer,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const query = new URLSearchParams(window.location.search);
    const resultOrder = query.get('order');
    if (query.get('reset') === '1') {
      this.view = 'shop';
      this.showAuth = true;
      this.authMode = 'reset';
      this.resetEmail = query.get('email') ?? '';
      this.resetToken = query.get('token') ?? '';
    }
    if (resultOrder) {
      this.view = 'order-result';
      this.orderResultMessage = query.has('failed')
        ? 'Ödeme tamamlanmadı. Siparişini tekrar deneyebilirsin.'
        : 'Ödeme sağlayıcısından dönüş alındı. Sipariş durumun güncelleniyor.';
      this.trackingNumber = resultOrder;
    }
    this.api
      .initialize()
      .pipe(catchError(() => of(void 0)))
      .subscribe(() => {
        forkJoin({
          storefront: this.api.getStorefront().pipe(catchError(() => of(null))),
          categories: this.api.getCategories().pipe(catchError(() => of([] as Category[]))),
          catalog: this.api
            .getProducts()
            .pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 24 }))),
        }).subscribe((result) => {
          this.storefront = result.storefront;
          this.demoBadge = result.storefront?.demoMode ?? true;
          this.sandboxBadge =
            !this.demoBadge && result.storefront?.providerEnvironment === 'sandbox';
          this.categories = result.categories;
          this.products = result.catalog.items;
          this.loading = false;
          this.markView();
          this.api
            .getMe()
            .pipe(catchError(() => of(null)))
            .subscribe((user) => {
              this.user = user;
              if (user) {
                this.loadWishlist();
                this.loadAddresses();
              }
              if (resultOrder) this.loadOrderByIdentity(resultOrder);
              this.markView();
            });
        });
      });
  }

  get filteredProducts(): Product[] {
    return this.products;
  }

  get cartCount(): number {
    return this.cart.reduce((sum, line) => sum + line.quantity, 0);
  }

  get subtotal(): number {
    return this.cart.reduce((sum, line) => sum + line.product.price * line.quantity, 0);
  }

  get discount(): number {
    return this.couponApplied ? Math.round(this.subtotal * this.couponPercent) / 100 : 0;
  }

  get shipping(): number {
    if (!this.storefront || this.subtotal - this.discount >= this.storefront.freeShippingThreshold)
      return 0;
    return this.storefront?.shippingFee ?? 0;
  }

  get total(): number {
    return Math.max(0, this.subtotal - this.discount) + this.shipping;
  }

  get amountToFreeShipping(): number {
    return Math.max(
      0,
      (this.storefront?.freeShippingThreshold ?? 1500) - (this.subtotal - this.discount),
    );
  }

  get shippingProgressPercent(): number {
    return Math.min(100, (this.subtotal / (this.storefront?.freeShippingThreshold || 1500)) * 100);
  }

  scrollToCatalog(): void {
    if (this.view !== 'shop') this.view = 'shop';
    setTimeout(() => document.getElementById('catalog')?.scrollIntoView({ behavior: 'smooth' }), 0);
  }

  loadCatalog(): void {
    this.loading = true;
    this.api
      .getProducts({
        q: this.searchTerm.trim(),
        categoryId: this.selectedCategoryId ?? undefined,
        sort: this.sort,
      })
      .subscribe({
        next: (page) => {
          this.products = page.items;
          this.loading = false;
          this.markView();
        },
        error: () => {
          this.errorMessage = 'Ürünleri şu an yükleyemedik. Birazdan tekrar deneyebilirsin.';
          this.loading = false;
          this.markView();
        },
      });
  }

  selectCategory(categoryId: number | null): void {
    this.selectedCategoryId = categoryId;
    this.loadCatalog();
  }

  addToCart(product: Product): void {
    if (product.stock < 1) return;
    const line = this.cart.find((item) => item.product.id === product.id);
    if (line) {
      if (line.quantity >= product.stock) {
        this.toast('Bu ürünün eldeki stoğuna ulaştın.');
        return;
      }
      line.quantity++;
    } else {
      this.cart.push({ product, quantity: 1 });
    }
    this.toast(`${product.name} sepete eklendi.`);
  }

  changeQuantity(line: CartLine, delta: number): void {
    const next = line.quantity + delta;
    if (next < 1) this.removeFromCart(line);
    else if (next <= line.product.stock) line.quantity = next;
    else this.toast('İstenen adet mevcut stoktan fazla.');
  }

  removeFromCart(line: CartLine): void {
    this.cart = this.cart.filter((item) => item !== line);
  }

  startCheckout(): void {
    if (!this.cart.length) return;
    this.checkout = {
      ...this.emptyCheckout(),
      customerName: this.user?.displayName ?? '',
      customerEmail: this.user?.email ?? '',
      items: [],
    };
    this.trackedOrder = null;
    this.showCart = false;
    this.showCheckout = true;
    this.paymentStage = 'idle';
    this.errorMessage = '';
    this.selectedAddressId = null;
    this.saveAddress = false;
    if (this.user) this.loadAddresses();
  }

  selectSavedAddress(id: number | null): void {
    this.selectedAddressId = id;
    const address = this.addresses.find((item) => item.id === id);
    if (address)
      this.checkout = {
        ...this.checkout,
        customerName: address.recipientName,
        customerPhone: address.phone,
        addressLine: address.addressLine,
        city: address.city,
        district: address.district,
        postalCode: address.postalCode,
      };
  }

  submitCheckout(): void {
    this.errorMessage = '';
    if (!this.cart.length) {
      this.errorMessage = 'Sepetin boş.';
      return;
    }
    if (
      this.checkout.provider === 'iyzico' &&
      !this.demoBadge &&
      (this.checkout.identityNumber?.length !== 11 ||
        !/^\d{11}$/.test(this.checkout.identityNumber))
    ) {
      this.errorMessage = 'iyzico ödemesi için 11 haneli T.C. kimlik numaranı gir.';
      return;
    }
    this.submitting = true;
    this.api
      .checkout({
        ...this.checkout,
        saveAddress: this.saveAddress,
        savedAddressId: this.selectedAddressId,
        couponCode: this.couponApplied || null,
        items: this.cart.map((line) => ({ productId: line.product.id, quantity: line.quantity })),
      })
      .subscribe({
        next: (result) => {
          this.submitting = false;
          this.checkoutResult = result;
          if (result.demoMode) {
            this.paymentStage = 'demo';
            this.markView();
            return;
          }
          if (result.embedUrl) {
            const url = new URL(result.embedUrl);
            if (url.hostname !== 'www.paytr.com') {
              this.errorMessage = 'Ödeme adresi doğrulanamadı.';
              this.markView();
              return;
            }
            this.paymentFrameUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url.toString());
            this.paymentStage = 'provider';
            this.markView();
          } else if (result.redirectUrl) {
            const url = new URL(result.redirectUrl);
            if (
              url.hostname !== 'sandbox-cpp.iyzipay.com' &&
              url.hostname !== 'api.iyzipay.com' &&
              url.hostname !== 'sandbox-api.iyzipay.com'
            ) {
              this.errorMessage = 'Ödeme adresi doğrulanamadı.';
              this.markView();
              return;
            }
            window.location.assign(url.toString());
          } else {
            this.errorMessage = 'Ödeme sağlayıcısından bir form adresi alınamadı.';
            this.markView();
          }
        },
        error: (error) => {
          this.submitting = false;
          this.errorMessage = this.readError(
            error,
            'Sipariş başlatılamadı. Bilgileri kontrol edip tekrar dene.',
          );
          this.markView();
        },
      });
  }

  settleDemoPayment(success: boolean): void {
    if (!this.checkoutResult) return;
    this.submitting = true;
    this.api.settleDemoPayment(this.checkoutResult.orderNumber, success).subscribe({
      next: () => {
        this.submitting = false;
        this.paymentStage = 'idle';
        this.showCheckout = false;
        this.orderResultMessage = success
          ? 'Siparişin alındı. Bu örnek siparişte ödeme ve kargo adımları canlandırıldı.'
          : 'Örnek ödeme reddedildi. Sipariş iptal edildi ve stok geri eklendi.';
        this.trackingNumber = this.checkoutResult!.orderNumber;
        this.trackingEmail = this.checkout.customerEmail;
        this.view = 'order-result';
        if (success) this.cart = [];
        this.couponApplied = '';
        this.loadCatalog();
        this.api.trackOrder(this.trackingNumber, this.trackingEmail).subscribe({
          next: (order) => {
            this.trackedOrder = order;
            this.markView();
          },
          error: () => {
            this.trackedOrder = null;
            this.markView();
          },
        });
        this.markView();
      },
      error: (error) => {
        this.submitting = false;
        this.errorMessage = this.readError(error, 'Örnek ödeme sonucu kaydedilemedi.');
        this.markView();
      },
    });
  }

  applyCoupon(): void {
    this.errorMessage = '';
    if (!this.couponInput.trim()) return;
    // The server remains the authority for the final discount.
    this.apiCoupon(this.couponInput.trim().toUpperCase());
  }

  private apiCoupon(code: string): void {
    // Keep a compact request path here so the server revalidates during order creation as well.
    this.api.checkCoupon(code, this.subtotal).subscribe({
      next: (result) => {
        this.couponApplied = result.code;
        this.couponPercent = result.percentOff;
        this.couponInput = result.code;
        this.toast('İndirim kodu eklendi.');
        this.markView();
      },
      error: () => {
        this.couponApplied = '';
        this.couponPercent = 0;
        this.errorMessage = 'Bu indirim kodu kullanılamıyor.';
        this.markView();
      },
    });
  }

  removeCoupon(): void {
    this.couponApplied = '';
    this.couponPercent = 0;
    this.couponInput = '';
  }

  openAuth(mode: 'login' | 'register' = 'login'): void {
    this.authMode = mode;
    this.authError = '';
    this.authMessage = '';
    this.showAuth = true;
  }

  submitAuth(): void {
    if (this.authMode === 'forgot') {
      this.submitForgotPassword();
      return;
    }
    if (this.authMode === 'reset') {
      this.submitPasswordReset();
      return;
    }
    this.authSubmitting = true;
    this.authError = '';
    const request =
      this.authMode === 'login'
        ? this.api.login(this.loginEmail, this.loginPassword, this.rememberMe)
        : this.api.register(this.registerName, this.registerEmail, this.registerPassword);
    request.subscribe({
      next: () =>
        this.api.initialize().subscribe({
          next: () =>
            this.api.getMe().subscribe({
              next: (user) => {
                this.user = user;
                this.authSubmitting = false;
                this.showAuth = false;
                this.loginPassword = '';
                this.registerPassword = '';
                this.loadWishlist();
                this.loadAddresses();
                this.toast(`Merhaba, ${user.displayName.split(' ')[0]}.`);
                this.markView();
              },
              error: () => {
                this.authSubmitting = false;
                this.authError = 'Hesap bilgileri yüklenemedi.';
                this.markView();
              },
            }),
          error: () => {
            this.authSubmitting = false;
            this.authError = 'Güvenlik doğrulaması yenilenemedi. Tekrar deneyin.';
            this.markView();
          },
        }),
      error: (error) => {
        this.authSubmitting = false;
        this.authError = this.readError(error, 'E-posta veya parola doğru değil.');
        this.markView();
      },
    });
  }

  private submitForgotPassword(): void {
    this.authSubmitting = true;
    this.authError = '';
    this.api.requestPasswordReset(this.loginEmail).subscribe({
      next: (result) => {
        this.authSubmitting = false;
        this.authMessage = result.message;
        if (result.demoResetToken) {
          this.resetEmail = this.loginEmail;
          this.resetToken = result.demoResetToken;
          this.authMode = 'reset';
        }
        this.markView();
      },
      error: () => {
        this.authSubmitting = false;
        this.authMessage = 'Hesabın varsa parola yenileme adımları e-posta adresine gönderildi.';
        this.markView();
      },
    });
  }

  private submitPasswordReset(): void {
    this.authSubmitting = true;
    this.authError = '';
    this.api.resetPassword(this.resetEmail, this.resetToken, this.newPassword).subscribe({
      next: () => {
        this.authSubmitting = false;
        this.authMode = 'login';
        this.loginEmail = this.resetEmail;
        this.newPassword = '';
        this.resetToken = '';
        this.authMessage = 'Parolan yenilendi. Şimdi hesabına giriş yapabilirsin.';
        this.markView();
      },
      error: (error) => {
        this.authSubmitting = false;
        this.authError = this.readError(error, 'Parola yenilenemedi. Yeni bir bağlantı iste.');
        this.markView();
      },
    });
  }

  signOut(): void {
    this.api.logout().subscribe({
      next: () => {
        this.user = null;
        this.view = 'shop';
        this.wishlistIds.clear();
        this.toast('Güvenli şekilde çıkış yaptın.');
      },
      error: () => this.toast('Çıkış yapılamadı.'),
    });
  }

  toggleWishlist(product: Product, event?: Event): void {
    event?.stopPropagation();
    if (!this.user) {
      this.openAuth('login');
      return;
    }
    const exists = this.wishlistIds.has(product.id);
    const action = exists ? this.api.removeWishlist(product.id) : this.api.addWishlist(product.id);
    action.subscribe({
      next: () => {
        exists ? this.wishlistIds.delete(product.id) : this.wishlistIds.add(product.id);
        this.wishlistIds = new Set(this.wishlistIds);
        this.toast(exists ? 'Favorilerinden kaldırıldı.' : 'Favorilerine eklendi.');
      },
      error: () => this.toast('Favori listesi güncellenemedi.'),
    });
  }

  showOrders(): void {
    if (!this.user) {
      this.openAuth('login');
      return;
    }
    this.view = 'orders';
    this.api.getMyOrders().subscribe({
      next: (orders) => {
        this.orders = orders;
        this.markView();
      },
      error: () => this.toast('Siparişler yüklenemedi.'),
    });
  }

  trackGuestOrder(): void {
    this.view = 'order-result';
    this.trackingNumber = '';
    this.trackingEmail = '';
    this.trackedOrder = null;
    this.orderResultMessage = 'Sipariş numaran ve e-posta adresinle durumunu öğrenebilirsin.';
  }

  showAdmin(): void {
    if (!this.user?.isAdmin) return;
    this.view = 'admin';
    forkJoin({
      summary: this.api.getAdminSummary(),
      orders: this.api.getAdminOrders(),
      products: this.api.getAdminProducts(),
      categories: this.api.getCategories(),
    }).subscribe({
      next: (result) => {
        this.adminSummary = result.summary;
        this.adminOrders = result.orders;
        this.adminProducts = result.products;
        this.categories = result.categories;
        if (!this.productDraft.categoryId)
          this.productDraft.categoryId = result.categories[0]?.id ?? 0;
        this.markView();
      },
      error: () => this.toast('Yönetim ekranı yüklenemedi.'),
    });
  }

  createAdminProduct(): void {
    this.api.createProduct(this.productDraft).subscribe({
      next: () => {
        this.productDraft = {
          ...this.productDraft,
          name: '',
          sku: '',
          description: '',
          price: 0,
          stock: 0,
          isFeatured: false,
        };
        this.toast('Ürün mağazaya eklendi.');
        this.showAdmin();
      },
      error: (error) =>
        this.toast(
          this.readError(error, 'Ürün eklenemedi. Ürün kodu ve kategori bilgilerini kontrol et.'),
        ),
    });
  }

  createAdminCategory(): void {
    this.api.createCategory(this.categoryDraft).subscribe({
      next: () => {
        this.categoryDraft = { name: '', description: '', accent: '#c47c5b' };
        this.toast('Kategori eklendi.');
        this.showAdmin();
      },
      error: (error) => this.toast(this.readError(error, 'Kategori eklenemedi.')),
    });
  }

  createAdminCoupon(): void {
    this.api.createCoupon(this.couponDraft).subscribe({
      next: () => {
        this.couponDraft = { ...this.couponDraft, code: '' };
        this.toast('İndirim kodu eklendi.');
        this.showAdmin();
      },
      error: (error) => this.toast(this.readError(error, 'İndirim kodu eklenemedi.')),
    });
  }

  updateAdminStatus(order: StoreOrder, status: string): void {
    this.api.updateOrderStatus(order.number, status).subscribe({
      next: () => {
        order.status = status;
        this.toast('Sipariş durumu güncellendi.');
      },
      error: () => this.toast('Sipariş durumu güncellenemedi.'),
    });
  }

  retryShipment(order: StoreOrder): void {
    this.api.createShipment(order.number).subscribe({
      next: (result) => {
        order.trackingNumber = result.trackingNumber;
        order.shippingStatus = result.shippingStatus;
        this.toast(
          result.trackingNumber ? `Kargo hazır · ${result.trackingNumber}` : 'Kargo oluşturuldu.',
        );
      },
      error: (error) =>
        this.toast(
          this.readError(error, 'Kargo oluşturulamadı; daha sonra yeniden deneyebilirsin.'),
        ),
    });
  }

  resendConfirmation(order: StoreOrder): void {
    this.api.resendConfirmationEmail(order.number).subscribe({
      next: () => this.toast('Sipariş e-postası yeniden gönderildi.'),
      error: () => this.toast('E-posta gönderilemedi.'),
    });
  }

  adjustStock(product: Product): void {
    const value = window.prompt(`${product.name} için yeni stok adedi`, String(product.stock));
    if (value === null) return;
    const stock = Number(value);
    if (!Number.isInteger(stock) || stock < 0) {
      this.toast('Stok için sıfır veya daha büyük tam sayı gir.');
      return;
    }
    this.api.updateProduct({ ...product, stock }).subscribe({
      next: () => {
        product.stock = stock;
        this.toast('Stok güncellendi.');
      },
      error: () => this.toast('Stok güncellenemedi.'),
    });
  }

  adjustPrice(product: Product): void {
    const value = window.prompt(`${product.name} için yeni fiyat (TL)`, String(product.price));
    if (value === null) return;
    const price = Number(value);
    if (!Number.isFinite(price) || price < 0) {
      this.toast('Fiyat için sıfır veya daha büyük bir tutar gir.');
      return;
    }
    this.api.updateProduct({ ...product, price }).subscribe({
      next: () => {
        product.price = price;
        this.toast('Fiyat güncellendi.');
      },
      error: () => this.toast('Fiyat güncellenemedi.'),
    });
  }

  trackOrder(): void {
    if (!this.trackingNumber.trim() || !this.trackingEmail.trim()) return;
    this.api.trackOrder(this.trackingNumber.trim(), this.trackingEmail.trim()).subscribe({
      next: (order) => {
        this.trackedOrder = order;
        this.orderResultMessage = '';
        this.markView();
      },
      error: () => {
        this.trackedOrder = null;
        this.orderResultMessage = 'Bu sipariş ve e-posta eşleşmesini bulamadık.';
        this.markView();
      },
    });
  }

  closeOverlays(): void {
    this.showCart = false;
    this.showAuth = false;
    this.showCheckout = false;
    this.selectedProduct = null;
    this.paymentStage = 'idle';
  }
  chooseView(view: StoreView): void {
    this.view = view;
    this.errorMessage = '';
    if (view === 'shop') this.loadCatalog();
  }
  formatPrice(value: number): string {
    return new Intl.NumberFormat('tr-TR', {
      style: 'currency',
      currency: 'TRY',
      maximumFractionDigits: 0,
    }).format(value);
  }
  productImage(product: Product): string {
    return product.imageUrl || '';
  }
  productInitial(product: Product): string {
    return product.name
      .split(/\s+/)
      .slice(0, 2)
      .map((word) => word[0])
      .join('');
  }

  orderStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      PendingPayment: 'Ödeme bekleniyor',
      Processing: 'Hazırlanıyor',
      Packed: 'Paketlendi',
      Shipped: 'Kargoda',
      Delivered: 'Teslim edildi',
      PaymentFailed: 'Ödeme alınamadı',
      Cancelled: 'İptal edildi',
      Expired: 'Süresi doldu',
    };
    return labels[status] ?? 'Durum güncelleniyor';
  }

  paymentStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Pending: 'Bekliyor',
      Paid: 'Alındı',
      Failed: 'Başarısız',
      Expired: 'Süresi doldu',
    };
    return labels[status] ?? 'Güncelleniyor';
  }

  shippingStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      NotCreated: 'Henüz oluşturulmadı',
      label_created: 'Kargo etiketi hazır',
      in_transit: 'Yolda',
      delivered: 'Teslim edildi',
      partially_delivered: 'Kısmen teslim edildi',
      failed: 'Teslimat sorunu var',
      returned: 'İade edildi',
    };
    return labels[status] ?? 'Durum güncelleniyor';
  }

  private loadWishlist(): void {
    this.api.getWishlist().subscribe({
      next: (items) => {
        this.wishlistIds = new Set(items.map((item) => item.id));
        this.markView();
      },
      error: () => undefined,
    });
  }

  private loadAddresses(): void {
    this.api.getAddresses().subscribe({
      next: (addresses) => {
        this.addresses = addresses;
        this.markView();
      },
      error: () => {
        this.addresses = [];
        this.markView();
      },
    });
  }

  private loadOrderByIdentity(number: string): void {
    if (this.user?.email) this.trackingEmail = this.user.email;
    if (this.trackingEmail) this.trackOrder();
  }

  private emptyCheckout(): CheckoutPayload {
    return {
      provider: 'iyzico',
      customerName: '',
      customerEmail: '',
      customerPhone: '',
      addressLine: '',
      city: '',
      district: '',
      postalCode: '',
      couponCode: null,
      notes: null,
      items: [],
    };
  }

  private readError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as {
        error?: string;
        title?: string;
        errors?: Record<string, string[]>;
      } | null;
      return (
        body?.error ??
        Object.values(body?.errors ?? {}).flat()[0] ??
        (error.status === 409
          ? 'Stok veya sipariş bilgisi değişmiş. Sepetini kontrol et.'
          : fallback)
      );
    }
    return fallback;
  }

  private toast(message: string): void {
    this.toastMessage = message;
    this.markView();
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => {
      this.toastMessage = '';
      this.markView();
    }, 2800);
  }

  private markView(): void {
    this.changeDetector.markForCheck();
  }
}
