import { TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './app';
import { Product, StoreApiService } from './store-api.service';

registerLocaleData(localeTr);

const product: Product = {
  id: 1,
  name: 'Seramik Kahve Seti',
  slug: 'seramik-kahve-seti',
  sku: 'BM-1001',
  description: 'Günlük kahve seti',
  price: 690,
  compareAtPrice: 820,
  stock: 1,
  categoryId: 1,
  category: 'Ev & Yaşam',
  imageUrl: '',
  imageTone: 'clay',
  isFeatured: true,
  weightKg: 0.8,
  widthCm: 18,
  lengthCm: 18,
  heightCm: 14,
};

describe('App', () => {
  const api = {
    initialize: vi.fn(() => of(void 0)),
    getStorefront: vi.fn(() =>
      of({
        demoMode: true,
        providerEnvironment: 'sandbox',
        currency: 'TRY',
        productCount: 1,
        freeShippingThreshold: 1500,
        shippingFee: 49,
      }),
    ),
    getCategories: vi.fn(() =>
      of([
        {
          id: 1,
          name: 'Ev & Yaşam',
          slug: 'ev-yasam',
          description: '',
          accent: '#c47c5b',
          productCount: 1,
        },
      ]),
    ),
    getProducts: vi.fn(() => of({ items: [product], total: 1, page: 1, pageSize: 24 })),
    getMe: vi.fn(() => of(null)),
    getWishlist: vi.fn(() => of([])),
    getMyOrders: vi.fn(() => of([])),
    getAdminSummary: vi.fn(() => of({ products: 1, lowStock: 0, pendingOrders: 0, revenue: 0 })),
    getAdminOrders: vi.fn(() => of([])),
    getAdminProducts: vi.fn(() => of([product])),
    getCoupon: vi.fn(() => of({ code: 'MERHABA10', percentOff: 10 })),
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: StoreApiService, useValue: api }],
    }).compileComponents();
  });

  afterEach(() => vi.useRealTimers());

  it('renders the Turkish storefront and demo marker', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Günün güzel');
    expect(compiled.textContent).toContain('SİMÜLASYON MAĞAZASI');
    expect(compiled.textContent).toContain('Seramik Kahve Seti');
  });

  it('keeps the cart within available stock', () => {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    app.addToCart(product);
    app.addToCart(product);
    expect(app.cartCount).toBe(1);
    expect(app.cart[0].quantity).toBe(1);
  });

  it('calculates subtotal, free shipping progress, and order total from the cart', () => {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    app.storefront = {
      demoMode: true,
      providerEnvironment: 'sandbox',
      currency: 'TRY',
      productCount: 1,
      freeShippingThreshold: 1500,
      shippingFee: 49,
    };
    app.addToCart(product);
    expect(app.subtotal).toBe(690);
    expect(app.shipping).toBe(49);
    expect(app.total).toBe(739);
    expect(app.amountToFreeShipping).toBe(810);
  });
});
