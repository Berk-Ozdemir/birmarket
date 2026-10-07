import { expect, test } from '@playwright/test';

test('demo customer can complete a simulated order', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Günün güzel/ })).toBeVisible();
  await expect(page.getByText('SİMÜLASYON MAĞAZASI')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'İyi şeyler burada.' })).toBeVisible();

  await page.locator('.product-card').first().hover();
  await page
    .locator('.product-card')
    .first()
    .getByRole('button', { name: /Sepete ekle/ })
    .click();
  await page.getByRole('button', { name: 'Sepeti aç' }).click();
  await expect(page.getByRole('heading', { name: /Sepetin/ })).toBeVisible();
  await page.getByRole('button', { name: /Alışverişi tamamla/ }).click();

  await page.getByLabel('Ad soyad').fill('Deniz Demir');
  await page.getByLabel('E-posta').fill('deniz@example.com');
  await page.getByLabel('Telefon').fill('05551234567');
  await page.getByLabel('Açık adres').fill('Moda Caddesi 10');
  await page.getByLabel('İl', { exact: true }).fill('İstanbul');
  await page.getByLabel('İlçe').fill('Kadıköy');
  await page.getByRole('button', { name: /Ödemeye geç/ }).click();

  await expect(page.getByRole('heading', { name: 'Ödeme nasıl sonuçlansın?' })).toBeVisible();
  await expect(page.getByText('Bu simülasyonda gerçek kart bilgisi alınmaz')).toBeVisible();
  await page.getByRole('button', { name: /Ödemeyi başarılı göster/ }).click();
  await expect(page.getByRole('heading', { name: 'Güzel bir seçim.' })).toBeVisible();
  await expect(page.locator('.tracking-live strong')).toHaveText(/ÖRNEK-\d+/);
});

test('catalog can be searched and filtered', async ({ page, isMobile }) => {
  await page.goto('/');
  if (isMobile) await page.getByRole('button', { name: 'Aramayı aç' }).click();
  await page.getByLabel('Ürün ara').fill('seramik');
  await expect(page.getByRole('heading', { name: 'Seramik Kahve Seti' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Dokulu Keten Çanta' })).toHaveCount(0);
});

test('mobile storefront keeps the product and cart actions usable', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'Mobile viewport is covered by the mobile project.');
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Sepeti aç' })).toBeVisible();
  await expect(page.locator('.product-card').first()).toBeVisible();
});

test('demo administrator can create a catalog product', async ({ page, isMobile }) => {
  test.skip(isMobile, 'Administrator form coverage uses the desktop viewport.');
  await page.goto('/');
  await page.locator('.header-actions').getByRole('button', { name: 'Hesabım' }).click();
  await page.getByLabel('E-posta').fill('admin@birmarket.local');
  await page.getByLabel('Parola').fill('DemoAdmin!234');
  await page.getByRole('button', { name: /Giriş yap/ }).click();
  await page.getByRole('button', { name: 'Yönetim' }).click();
  await expect(page.getByRole('heading', { name: 'İşler yolunda.' })).toBeVisible();

  const productName = `E2E Product ${Date.now()}`;
  await page.getByLabel('Ürün adı').fill(productName);
  await page.getByLabel('Ürün kodu').fill(`E2E-${Date.now()}`);
  await page.locator('.admin-editor').getByLabel('Kategori').selectOption({ index: 1 });
  await page.getByLabel('Fiyat (TL)').fill('125');
  await page.getByLabel('Stok').fill('5');
  await page
    .locator('.admin-editor')
    .getByLabel('Açıklama')
    .fill('Created by the isolated browser test.');
  await page.getByRole('button', { name: /Ürünü ekle/ }).click();
  await expect(page.getByText(productName)).toBeVisible();
});

test('demo customer can reset a forgotten password and sign in', async ({ page, isMobile }) => {
  test.skip(isMobile, 'Account recovery coverage uses the desktop viewport.');
  await page.goto('/');
  await page.locator('.header-actions').getByRole('button', { name: 'Hesabım' }).click();
  await page.getByRole('button', { name: 'Parolamı unuttum' }).click();
  await page.getByLabel('E-posta').fill('customer@birmarket.local');
  await page.getByRole('button', { name: /Yenileme bağlantısı gönder/ }).click();
  await page.getByLabel('Yeni parola').fill('NewDemoPassword!123');
  await page.getByRole('button', { name: /Parolayı güncelle/ }).click();
  await expect(
    page.getByText('Parolan yenilendi. Şimdi hesabına giriş yapabilirsin.'),
  ).toBeVisible();
  await page.getByLabel('Parola').fill('NewDemoPassword!123');
  await page.getByRole('button', { name: /Giriş yap/ }).click();
  await expect(page.getByRole('button', { name: 'Siparişlerim' })).toBeVisible();
});
