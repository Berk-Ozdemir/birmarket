import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { StoreApiService } from './store-api.service';

describe('StoreApiService', () => {
  let api: StoreApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(StoreApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the catalog resource and serializes search filters', () => {
    api.getProducts({ q: 'seramik', categoryId: 3, sort: 'price-asc' }).subscribe();
    const request = http.expectOne((request) => request.url === '/api/catalog/products');
    expect(request.request.params.get('q')).toBe('seramik');
    expect(request.request.params.get('categoryId')).toBe('3');
    expect(request.request.params.get('sort')).toBe('price-asc');
    request.flush({ items: [], total: 0, page: 1, pageSize: 24 });
  });

  it('submits item identifiers and quantities rather than client prices', () => {
    const payload = {
      provider: 'iyzico' as const,
      customerName: 'Deniz Demir',
      customerEmail: 'deniz@example.com',
      customerPhone: '05551234567',
      addressLine: 'Moda Caddesi 10',
      city: 'İstanbul',
      district: 'Kadıköy',
      postalCode: '',
      couponCode: null,
      notes: null,
      items: [{ productId: 12, quantity: 2 }],
    };
    api.checkout(payload).subscribe();
    const request = http.expectOne('/api/orders');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.items).toEqual([{ productId: 12, quantity: 2 }]);
    expect(request.request.body.total).toBeUndefined();
    request.flush({
      orderNumber: 'BM260101123',
      total: 100,
      paymentProvider: 'iyzico',
      demoMode: true,
      redirectUrl: null,
      embedUrl: null,
      status: 'Pending',
    });
  });
});
