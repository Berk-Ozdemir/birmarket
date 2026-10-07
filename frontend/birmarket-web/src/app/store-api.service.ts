import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface Category {
  id: number;
  name: string;
  slug: string;
  description: string;
  accent: string;
  productCount: number;
}

export interface Product {
  id: number;
  name: string;
  slug: string;
  sku: string;
  description: string;
  price: number;
  compareAtPrice: number | null;
  stock: number;
  categoryId: number;
  category: string;
  imageUrl: string;
  imageTone: string;
  isFeatured: boolean;
  weightKg: number;
  widthCm: number;
  lengthCm: number;
  heightCm: number;
  isActive?: boolean;
}

export interface CatalogPage {
  items: Product[];
  total: number;
  page: number;
  pageSize: number;
}

export interface StorefrontInfo {
  demoMode: boolean;
  providerEnvironment: 'sandbox' | 'live';
  currency: string;
  productCount: number;
  freeShippingThreshold: number;
  shippingFee: number;
}

export interface StoreUser {
  id: string;
  email: string;
  displayName: string;
  isAdmin: boolean;
}

export interface CustomerAddress {
  id: number;
  label: string;
  recipientName: string;
  phone: string;
  addressLine: string;
  city: string;
  district: string;
  postalCode: string;
  isDefault: boolean;
}

export interface CartLine {
  product: Product;
  quantity: number;
}

export interface CheckoutPayload {
  provider: 'iyzico' | 'paytr';
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  addressLine: string;
  city: string;
  district: string;
  postalCode: string;
  couponCode: string | null;
  notes: string | null;
  items: { productId: number; quantity: number }[];
  identityNumber?: string;
  saveAddress?: boolean;
  savedAddressId?: number | null;
}

export interface CheckoutResult {
  orderNumber: string;
  total: number;
  paymentProvider: string;
  demoMode: boolean;
  redirectUrl: string | null;
  embedUrl: string | null;
  status: string;
}

export interface StoreOrder {
  number: string;
  status: string;
  paymentStatus: string;
  shippingStatus: string;
  trackingNumber: string | null;
  total: number;
  createdAt: string;
  customerName?: string;
  customerEmail?: string;
  items: { productName: string; quantity: number; unitPrice: number; imageUrl?: string }[];
}

@Injectable({ providedIn: 'root' })
export class StoreApiService {
  private readonly api = '/api';

  constructor(private readonly http: HttpClient) {}

  initialize(): Observable<void> {
    return this.http.get<void>(`${this.api}/auth/csrf`);
  }

  getStorefront(): Observable<StorefrontInfo> {
    return this.http.get<StorefrontInfo>(`${this.api}/storefront`);
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.api}/catalog/categories`);
  }

  getProducts(
    filters: { q?: string; categoryId?: number; sort?: string; featured?: boolean } = {},
  ): Observable<CatalogPage> {
    let params = new HttpParams();
    if (filters.q) params = params.set('q', filters.q);
    if (filters.categoryId) params = params.set('categoryId', filters.categoryId);
    if (filters.sort) params = params.set('sort', filters.sort);
    if (filters.featured) params = params.set('featured', true);
    return this.http.get<CatalogPage>(`${this.api}/catalog/products`, { params });
  }

  getMe(): Observable<StoreUser> {
    return this.http.get<StoreUser>(`${this.api}/auth/me`);
  }

  register(displayName: string, email: string, password: string): Observable<unknown> {
    return this.http.post(`${this.api}/auth/register`, { displayName, email, password });
  }

  requestPasswordReset(email: string): Observable<{ message: string; demoResetToken?: string }> {
    return this.http.post<{ message: string; demoResetToken?: string }>(
      `${this.api}/auth/password/forgot`,
      { email },
    );
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/password/reset`, { email, token, newPassword });
  }

  login(email: string, password: string, rememberMe = false): Observable<unknown> {
    return this.http.post(`${this.api}/auth/login`, { email, password, rememberMe });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/logout`, {});
  }

  checkout(payload: CheckoutPayload): Observable<CheckoutResult> {
    return this.http.post<CheckoutResult>(`${this.api}/orders`, payload);
  }

  checkCoupon(code: string, subtotal: number): Observable<{ code: string; percentOff: number }> {
    return this.http.post<{ code: string; percentOff: number }>(`${this.api}/orders/coupon`, {
      code,
      subtotal,
    });
  }

  settleDemoPayment(
    orderNumber: string,
    success: boolean,
  ): Observable<{ orderNumber: string; paymentStatus: string }> {
    return this.http.post<{ orderNumber: string; paymentStatus: string }>(
      `${this.api}/orders/${encodeURIComponent(orderNumber)}/demo-payment`,
      { success },
    );
  }

  getMyOrders(): Observable<StoreOrder[]> {
    return this.http.get<StoreOrder[]>(`${this.api}/orders/mine`);
  }

  getAddresses(): Observable<CustomerAddress[]> {
    return this.http.get<CustomerAddress[]>(`${this.api}/account/addresses`);
  }

  saveAddress(address: Omit<CustomerAddress, 'id'>): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.api}/account/addresses`, address);
  }

  trackOrder(number: string, email: string): Observable<StoreOrder> {
    const params = new HttpParams().set('email', email);
    return this.http.get<StoreOrder>(`${this.api}/orders/${encodeURIComponent(number)}`, {
      params,
    });
  }

  getWishlist(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.api}/wishlist`);
  }

  addWishlist(productId: number): Observable<void> {
    return this.http.post<void>(`${this.api}/wishlist/${productId}`, {});
  }

  removeWishlist(productId: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/wishlist/${productId}`);
  }

  getAdminSummary(): Observable<{
    products: number;
    lowStock: number;
    pendingOrders: number;
    revenue: number;
  }> {
    return this.http.get<{
      products: number;
      lowStock: number;
      pendingOrders: number;
      revenue: number;
    }>(`${this.api}/admin/summary`);
  }

  getAdminOrders(): Observable<StoreOrder[]> {
    return this.http.get<StoreOrder[]>(`${this.api}/admin/orders`);
  }

  getAdminProducts(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.api}/admin/products`);
  }

  updateOrderStatus(number: string, status: string): Observable<void> {
    return this.http.put<void>(`${this.api}/admin/orders/${encodeURIComponent(number)}/status`, {
      status,
    });
  }

  createShipment(
    number: string,
  ): Observable<{ shipmentId: string; trackingNumber: string | null; shippingStatus: string }> {
    return this.http.post<{
      shipmentId: string;
      trackingNumber: string | null;
      shippingStatus: string;
    }>(`${this.api}/admin/orders/${encodeURIComponent(number)}/shipment`, {});
  }

  resendConfirmationEmail(number: string): Observable<void> {
    return this.http.post<void>(
      `${this.api}/admin/orders/${encodeURIComponent(number)}/confirmation-email`,
      {},
    );
  }

  updateProduct(product: Product): Observable<void> {
    const {
      name,
      sku,
      description,
      price,
      compareAtPrice,
      stock,
      categoryId,
      imageUrl,
      imageTone,
      weightKg,
      widthCm,
      lengthCm,
      heightCm,
      isFeatured,
      isActive,
    } = product;
    return this.http.put<void>(`${this.api}/admin/products/${product.id}`, {
      name,
      sku,
      description,
      price,
      compareAtPrice,
      stock,
      categoryId,
      imageUrl,
      imageTone,
      weightKg,
      widthCm,
      lengthCm,
      heightCm,
      isFeatured,
      isActive,
    });
  }

  createProduct(
    product: Omit<Product, 'id' | 'slug' | 'category'>,
  ): Observable<{ id: number; slug: string }> {
    return this.http.post<{ id: number; slug: string }>(`${this.api}/admin/products`, product);
  }

  createCategory(category: {
    name: string;
    description: string;
    accent: string;
  }): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.api}/admin/categories`, category);
  }

  createCoupon(coupon: {
    code: string;
    percentOff: number;
    minimumSubtotal: number | null;
    expiresAt: string | null;
    maximumUses: number | null;
    isActive: boolean;
  }): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.api}/admin/coupons`, coupon);
  }
}
