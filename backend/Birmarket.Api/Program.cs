using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Birmarket.Api.Configuration;
using Birmarket.Api.Contracts;
using Birmarket.Api.Data;
using Birmarket.Api.Domain;
using Birmarket.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Birmarket.Api;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        EnvironmentFile.Load();
        var builder = WebApplication.CreateBuilder(args);
        var runtime = AppRuntimeOptions.From(builder.Configuration);
        var missing = runtime.MissingRealSettings();
        if (missing.Count > 0)
            throw new InvalidOperationException("Gerçek mod başlatılamadı. birmarket.env içinde şu ayarları tamamlayın: " + string.Join(", ", missing));

        Directory.CreateDirectory(Path.GetDirectoryName(runtime.DatabasePath)!);
        builder.Services.AddSingleton(runtime);
        builder.Services.AddDataProtection()
            .SetApplicationName(runtime.DemoMode ? "Birmarket.Demo" : "Birmarket.Real")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetDirectoryName(runtime.DatabasePath)!, runtime.DemoMode ? "keys-demo" : "keys-real")));
        builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new EnvironmentXmlEncryptor(runtime.DataProtectionKey));
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={runtime.DatabasePath};Foreign Keys=True;Cache=Shared"));
        builder.Services.AddIdentityCore<CustomerUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 6;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = runtime.DemoMode ? "Birmarket.Auth.Demo" : "Birmarket.Auth.Real";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = "/account";
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy("checkout", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy("provider-callback", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = runtime.DemoMode ? "Birmarket.Antiforgery.Demo" : "Birmarket.Antiforgery.Real";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.HeaderName = "X-XSRF-TOKEN";
        });
        builder.Services.AddOpenApi();
        builder.Services.AddHttpClient<PayTrPaymentProvider>();
        builder.Services.AddHttpClient<IyzicoPaymentProvider>();
        builder.Services.AddHttpClient<KargoJetShippingProvider>(client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddScoped<DemoSeeder>();
        builder.Services.AddScoped<OrderWorkflow>();
        builder.Services.AddHostedService<OrderReservationCleanupService>();
        if (runtime.DemoMode)
        {
            builder.Services.AddScoped<IShippingProvider, DemoShippingProvider>();
            builder.Services.AddScoped<IEmailSender, DemoEmailSender>();
        }
        else
        {
            builder.Services.AddScoped<IShippingProvider, KargoJetShippingProvider>();
            builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        builder.Services.AddScoped<PaymentProviderRegistry>(services => new PaymentProviderRegistry(
        [
            services.GetRequiredService<PayTrPaymentProvider>(),
            services.GetRequiredService<IyzicoPaymentProvider>(),
            services.GetRequiredService<DemoPaymentProvider>()
        ]));
        builder.Services.AddSingleton<DemoPaymentProvider>();
        builder.Services.AddCors(options => options.AddPolicy("development", policy => policy
            .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        var app = builder.Build();
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        app.UseExceptionHandler(error => error.Run(async context =>
        {
            var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            if (exception is not null)
                context.RequestServices.GetRequiredService<ILogger<Program>>().LogError(exception, "Unhandled API error for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new { title = "İşlem tamamlanamadı.", status = 500 });
        }));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            await next();
        });
        app.UseStaticFiles();
        app.UseRouting();
        if (app.Environment.IsDevelopment()) app.UseCors("development");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            var method = context.Request.Method;
            var mutating = method is "POST" or "PUT" or "PATCH" or "DELETE";
            var externalCallback = context.Request.Path.StartsWithSegments("/api/payments/") && context.Request.Path.Value?.EndsWith("callback", StringComparison.OrdinalIgnoreCase) == true
                || context.Request.Path.StartsWithSegments("/api/webhooks/kargojet");
            if (mutating && context.Request.Path.StartsWithSegments("/api") && !externalCallback)
            {
                try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
                catch (AntiforgeryValidationException)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "Güvenlik doğrulaması geçersiz. Sayfayı yenileyip tekrar deneyin." });
                    return;
                }
            }
            await next();
        });

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
        }

        MapRoutes(app, runtime);
        app.MapFallbackToFile("index.html");
        await app.RunAsync();
    }

    public static void MapRoutes(WebApplication app, AppRuntimeOptions runtime)
    {
        app.MapGet("/api/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/health/ready", async (AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                return Results.Ok(new { status = "ready", mode = runtime.DemoMode ? "demo" : "real" });
            }
            catch { return Results.Problem("Veritabanı hazır değil.", statusCode: 503); }
        });

        app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                IsEssential = true
            });
            return Results.NoContent();
        });
        app.MapPost("/api/auth/register", async (RegisterRequest request, UserManager<CustomerUser> users, SignInManager<CustomerUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 120 || !IsEmail(request.Email) || string.IsNullOrEmpty(request.Password) || request.Password.Length < 10)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["register"] = ["Ad, geçerli e-posta ve en az 10 karakterli parola girin."] });
            var user = new CustomerUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded) return Results.ValidationProblem(created.Errors.ToDictionary(x => x.Code, x => new[] { x.Description }));
            await users.AddToRoleAsync(user, "Customer");
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.Created("/api/auth/me", new { email = user.Email, displayName = user.DisplayName });
        }).RequireRateLimiting("authentication");
        app.MapPost("/api/auth/login", async (LoginRequest request, SignInManager<CustomerUser> signIn) =>
        {
            if (!IsEmail(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, request.RememberMe, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok(new { signedIn = true }) : Results.Unauthorized();
        }).RequireRateLimiting("authentication");
        app.MapPost("/api/auth/password/forgot", async (ForgotPasswordRequest request, UserManager<CustomerUser> users, IEmailSender email, AppRuntimeOptions settings, ILogger<Program> logger, CancellationToken ct) =>
        {
            const string message = "Hesabın varsa parola yenileme adımları e-posta adresine gönderildi.";
            if (!IsEmail(request.Email)) return Results.Ok(new { message });
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.Ok(new { message });
            var rawToken = await users.GeneratePasswordResetTokenAsync(user);
            var resetToken = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(rawToken));
            var resetUrl = $"{settings.PublicBaseUrl.TrimEnd('/')}/?reset=1&email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(resetToken)}";
            var body = $"<h1>Parolanı yenile</h1><p>Birmarket hesabın için yeni bir parola oluşturmak üzere <a href=\"{System.Net.WebUtility.HtmlEncode(resetUrl)}\">bu bağlantıyı aç</a>.</p><p>Bu isteği sen yapmadıysan bu iletiyi yok sayabilirsin.</p>";
            try { await email.SendAsync(user.Email!, "Birmarket parolani yenile", body, ct); }
            catch (Exception exception) { logger.LogError("Password reset email failed: {ErrorType}", exception.GetType().Name); }
            return settings.DemoMode ? Results.Ok(new { message, demoResetToken = resetToken }) : Results.Ok(new { message });
        }).RequireRateLimiting("authentication");
        app.MapPost("/api/auth/password/reset", async (ResetPasswordRequest request, UserManager<CustomerUser> users, CancellationToken ct) =>
        {
            if (!IsEmail(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 10)
                return Results.BadRequest(new { error = "Parola yenileme bilgilerini kontrol edin." });
            string token;
            try { token = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
            catch (FormatException) { return Results.BadRequest(new { error = "Bu parola yenileme bağlantısı geçersiz veya süresi dolmuş." }); }
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.BadRequest(new { error = "Bu parola yenileme bağlantısı geçersiz veya süresi dolmuş." });
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded) return Results.ValidationProblem(result.Errors.ToDictionary(x => x.Code, x => new[] { x.Description }));
            await users.UpdateSecurityStampAsync(user);
            return Results.NoContent();
        }).RequireRateLimiting("authentication");
        app.MapPost("/api/auth/logout", async (SignInManager<CustomerUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<CustomerUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            return user is null ? Results.Unauthorized() : Results.Ok(new { id = user.Id, email = user.Email, displayName = user.DisplayName, isAdmin = await users.IsInRoleAsync(user, "Admin") });
        });

        app.MapGet("/api/catalog/categories", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Categories.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Slug, x.Description, x.Accent, productCount = x.Products.Count(p => p.IsActive) }).ToListAsync(ct)));
        app.MapGet("/api/catalog/products", async (AppDbContext db, string? q, int? categoryId, string? sort, bool? featured, int? page, int? pageSize, CancellationToken ct) =>
        {
            var query = db.Products.AsNoTracking().Include(x => x.Category).Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term) || x.SKU.ToLower().Contains(term) || x.Category!.Name.ToLower().Contains(term));
            }
            if (categoryId is > 0) query = query.Where(x => x.CategoryId == categoryId);
            if (featured == true) query = query.Where(x => x.IsFeatured);
            query = sort switch { "price-asc" => query.OrderBy(x => x.Price), "price-desc" => query.OrderByDescending(x => x.Price), "name" => query.OrderBy(x => x.Name), _ => query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.Name) };
            var total = await query.CountAsync(ct);
            var currentPage = Math.Max(1, page ?? 1);
            var take = Math.Clamp(pageSize ?? 24, 1, 48);
            var products = await query.Skip((currentPage - 1) * take).Take(take)
                .Select(x => new { x.Id, x.Name, x.Slug, x.SKU, x.Description, x.Price, x.CompareAtPrice, x.Stock, x.CategoryId, category = x.Category!.Name, x.ImageUrl, x.ImageTone, x.IsFeatured, x.WeightKg, x.WidthCm, x.LengthCm, x.HeightCm })
                .ToListAsync(ct);
            return Results.Ok(new { items = products, total, page = currentPage, pageSize = take });
        });
        app.MapGet("/api/catalog/products/{slug}", async (string slug, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, ct);
            return product is null ? Results.NotFound() : Results.Ok(new { product.Id, product.Name, product.Slug, product.SKU, product.Description, product.Price, product.CompareAtPrice, product.Stock, product.CategoryId, category = product.Category!.Name, product.ImageUrl, product.ImageTone, product.IsFeatured });
        });
        app.MapGet("/api/storefront", async (AppDbContext db, AppRuntimeOptions settings, CancellationToken ct) =>
        {
            var count = await db.Products.CountAsync(x => x.IsActive, ct);
            return Results.Ok(new { demoMode = settings.DemoMode, providerEnvironment = settings.PaymentEnvironment, currency = "TRY", productCount = count, freeShippingThreshold = settings.FreeShippingThreshold, shippingFee = settings.FlatShippingFee });
        });

        app.MapPost("/api/orders/coupon", async (CouponCheckRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code == request.Code.Trim().ToUpperInvariant() && x.IsActive, ct);
            if (coupon is null || coupon.PercentOff >= 100 || request.Subtotal < 0 || (coupon.ExpiresAt is not null && coupon.ExpiresAt < DateTimeOffset.UtcNow) || (coupon.MaximumUses is not null && coupon.Uses >= coupon.MaximumUses) || request.Subtotal < (coupon.MinimumSubtotal ?? 0))
                return Results.BadRequest(new { error = "Bu indirim kodu kullanılamıyor." });
            var discount = decimal.Round(request.Subtotal * coupon.PercentOff / 100, 2);
            return Results.Ok(new { code = coupon.Code, percentOff = coupon.PercentOff, discount });
        });
        app.MapPost("/api/orders", async (CheckoutRequest request, HttpContext context, AppDbContext db, AppRuntimeOptions settings, PaymentProviderRegistry providers, OrderWorkflow workflow, ClaimsPrincipal principal, UserManager<CustomerUser> users, CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(principal);
            if (request.SavedAddressId is int savedAddressId)
            {
                if (user is null) return Results.Unauthorized();
                var saved = await db.CustomerAddresses.FirstOrDefaultAsync(x => x.Id == savedAddressId && x.UserId == user.Id, ct);
                if (saved is null) return Results.NotFound();
                request = request with { CustomerName = saved.RecipientName, CustomerPhone = saved.Phone, AddressLine = saved.AddressLine, City = saved.City, District = saved.District, PostalCode = saved.PostalCode };
            }
            if (request.Items is null || request.Items.Count == 0 || request.Items.Any(x => x.ProductId < 1 || x.Quantity is < 1 or > 20) || request.Items.Select(x => x.ProductId).Distinct().Count() != request.Items.Count)
                return Results.BadRequest(new { error = "Sepet boş veya ürün adedi geçersiz." });
            if (!IsEmail(request.CustomerEmail) || string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone) || string.IsNullOrWhiteSpace(request.AddressLine) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.District))
                return Results.BadRequest(new { error = "Teslimat ve iletişim bilgilerini kontrol edin." });
            if (request.Provider is not ("iyzico" or "paytr")) return Results.BadRequest(new { error = "Bir ödeme sağlayıcısı seçin." });
            if (request.Provider == "paytr" && request.CustomerEmail.Any(character => character > 127))
                return Results.BadRequest(new { error = "PayTR için e-posta adresinde Türkçe karakter bulunmamalı." });
            if (!settings.DemoMode && request.Provider == "iyzico" && (request.IdentityNumber?.Length != 11 || !request.IdentityNumber.All(char.IsDigit)))
                return Results.BadRequest(new { error = "iyzico ödemesi için 11 haneli T.C. kimlik numarası gereklidir." });

            StoreOrder order;
            await using (var transaction = await db.Database.BeginTransactionAsync(ct))
            {
                var products = await db.Products.Where(p => request.Items.Select(i => i.ProductId).Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id, ct);
                if (products.Count != request.Items.Select(i => i.ProductId).Distinct().Count()) return Results.BadRequest(new { error = "Sepette artık bulunmayan bir ürün var." });
                foreach (var line in request.Items)
                    if (products[line.ProductId].Stock < line.Quantity) return Results.Conflict(new { error = $"{products[line.ProductId].Name} için yeterli stok yok." });
                var subtotal = request.Items.Sum(line => products[line.ProductId].Price * line.Quantity);
                var discount = 0m;
                string? couponCode = null;
                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {
                    var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code == request.CouponCode.Trim().ToUpperInvariant() && x.IsActive, ct);
                    if (coupon is null || coupon.PercentOff >= 100 || (coupon.ExpiresAt is not null && coupon.ExpiresAt < DateTimeOffset.UtcNow) || (coupon.MaximumUses is not null && coupon.Uses >= coupon.MaximumUses) || subtotal < (coupon.MinimumSubtotal ?? 0))
                        return Results.BadRequest(new { error = "İndirim kodu artık kullanılamıyor." });
                    discount = decimal.Round(subtotal * coupon.PercentOff / 100, 2);
                    coupon.Uses++;
                    couponCode = coupon.Code;
                }
                var shipping = subtotal - discount >= settings.FreeShippingThreshold ? 0 : settings.FlatShippingFee;
                var number = "BM" + DateTime.UtcNow.ToString("yyMMdd", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
                order = new StoreOrder
                {
                    Number = number,
                    UserId = user?.Id,
                    CustomerName = request.CustomerName.Trim(),
                    CustomerEmail = request.CustomerEmail.Trim(),
                    CustomerPhone = request.CustomerPhone.Trim(),
                    AddressLine = request.AddressLine.Trim(),
                    City = request.City.Trim(),
                    District = request.District.Trim(),
                    PostalCode = request.PostalCode?.Trim() ?? "",
                    Notes = request.Notes?.Trim(),
                    Subtotal = subtotal,
                    Discount = discount,
                    Shipping = shipping,
                    Total = subtotal - discount + shipping,
                    CouponCode = couponCode,
                    PaymentProvider = request.Provider,
                    ProviderReference = number,
                    Items = request.Items.Select(line =>
                    {
                        var product = products[line.ProductId];
                        product.Stock -= line.Quantity;
                        return new StoreOrderItem { ProductId = product.Id, ProductName = product.Name, SKU = product.SKU, ImageUrl = product.ImageUrl, UnitPrice = product.Price, Quantity = line.Quantity, WeightKg = product.WeightKg, WidthCm = product.WidthCm, LengthCm = product.LengthCm, HeightCm = product.HeightCm };
                    }).ToList()
                };
                db.Orders.Add(order);
                if (user is not null && request.SaveAddress && !await db.CustomerAddresses.AnyAsync(x => x.UserId == user.Id && x.AddressLine == order.AddressLine && x.City == order.City && x.District == order.District, ct))
                {
                    var isFirst = !await db.CustomerAddresses.AnyAsync(x => x.UserId == user.Id, ct);
                    db.CustomerAddresses.Add(new CustomerAddress { UserId = user.Id, Label = "Ev", RecipientName = order.CustomerName, Phone = order.CustomerPhone, AddressLine = order.AddressLine, City = order.City, District = order.District, PostalCode = order.PostalCode, IsDefault = isFirst });
                }
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }

            try
            {
                var provider = settings.DemoMode ? providers.Get("demo") : providers.Get(request.Provider);
                var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
                var payment = await provider.StartAsync(new PaymentStartRequest(order, ip, request.IdentityNumber), ct);
                if (!settings.DemoMode && request.Provider == "iyzico" && string.IsNullOrWhiteSpace(payment.ProviderToken))
                    throw new InvalidOperationException("iyzico did not return a checkout token.");
                order.ProviderToken = payment.ProviderToken;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { orderNumber = order.Number, total = order.Total, paymentProvider = request.Provider, demoMode = settings.DemoMode, redirectUrl = payment.RedirectUrl, embedUrl = payment.EmbedUrl, status = order.PaymentStatus });
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Payment initialization failed for order {order.Number}: {exception.GetType().Name}");
                await workflow.SettleAsync(order.Number, request.Provider, false, null, $"{order.Number}:initialization-failed", ct);
                return Results.Problem("Ödeme oturumu başlatılamadı. Sepetteki ürünler yeniden stoğa eklendi.", statusCode: 502);
            }
        }).RequireRateLimiting("checkout");
        app.MapPost("/api/orders/{number}/demo-payment", async (string number, DemoPaymentRequest request, AppDbContext db, PaymentProviderRegistry providers, OrderWorkflow workflow, CancellationToken ct) =>
        {
            if (!runtime.DemoMode) return Results.NotFound();
            var order = await db.Orders.FirstOrDefaultAsync(x => x.Number == number, ct);
            if (order is null) return Results.NotFound();
            await workflow.SettleAsync(order.Number, order.PaymentProvider, request.Success, request.Success ? "DEMO-PAID" : null, $"{order.Number}:demo-result-{(request.Success ? "success" : "failed")}", ct);
            return Results.Ok(new { orderNumber = order.Number, paymentStatus = request.Success ? "Paid" : "Failed" });
        });
        app.MapPost("/api/payments/paytr/callback", async (HttpRequest request, AppDbContext db, PaymentProviderRegistry providers, OrderWorkflow workflow, CancellationToken ct) =>
        {
            if (request.ContentLength is > 16_384) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var form = await request.ReadFormAsync(ct);
            var merchantOid = form["merchant_oid"].ToString();
            var status = form["status"].ToString();
            var totalAmount = form["total_amount"].ToString();
            var hash = form["hash"].ToString();
            var gateway = providers.Get("paytr");
            if (!gateway.ValidatePayTrCallback(merchantOid, status, totalAmount, hash)) return Results.Text("PAYTR notification failed", statusCode: 400);
            var order = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Number == merchantOid, ct);
            if (order is null) return Results.Text("PAYTR notification failed", statusCode: 400);
            if (!long.TryParse(totalAmount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var received) || received < decimal.ToInt64(decimal.Round(order.Total * 100, 0)))
                return Results.Text("PAYTR notification failed", statusCode: 400);
            await workflow.SettleAsync(merchantOid, "paytr", status == "success", form["failed_reason_code"].ToString(), $"{merchantOid}:{status}:{totalAmount}", ct);
            return Results.Text("OK");
        }).RequireRateLimiting("provider-callback");
        app.MapPost("/api/payments/iyzico/callback", async (HttpRequest request, AppDbContext db, PaymentProviderRegistry providers, OrderWorkflow workflow, CancellationToken ct) =>
        {
            if (request.ContentLength is > 16_384) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var form = await request.ReadFormAsync(ct);
            var token = form["token"].ToString();
            if (string.IsNullOrWhiteSpace(token)) return Results.BadRequest();
            var order = await db.Orders.FirstOrDefaultAsync(x => x.ProviderToken == token && x.PaymentProvider == "iyzico", ct);
            if (order is null) return Results.BadRequest();
            var result = await providers.Get("iyzico").ConfirmIyzicoAsync(order.Number, token, ct);
            if (result.ConversationId != order.Number || result.Price is null || Math.Abs(result.Price.Value - order.Total) > 0.01m)
                return Results.BadRequest();
            await workflow.SettleAsync(order.Number, "iyzico", result.Success, result.TransactionId, result.EventKey, ct, result.Pending);
            var outcome = result.Pending ? "pending=1" : result.Success ? "paid=1" : "failed=1";
            return Results.Redirect($"{runtime.PublicBaseUrl}/checkout/result?order={Uri.EscapeDataString(order.Number)}&{outcome}");
        }).RequireRateLimiting("provider-callback");
        app.MapPost("/api/webhooks/kargojet", async (HttpRequest request, AppDbContext db, AppRuntimeOptions settings, CancellationToken ct) =>
        {
            if (request.ContentLength is > 65_536) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            using var stream = new MemoryStream();
            var buffer = new byte[8_192];
            while (true)
            {
                var bytesRead = await request.Body.ReadAsync(buffer, ct);
                if (bytesRead == 0) break;
                if (stream.Length + bytesRead > 65_536) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }
            var payload = stream.ToArray();
            var signature = request.Headers["X-KargoJet-Signature"].ToString();
            if (!OrderWorkflow.VerifyKargoJetSignature(payload, signature, settings.KargoJetWebhookSecret)) return Results.Unauthorized();
            var eventId = request.Headers["X-KargoJet-Event-Id"].ToString();
            if (string.IsNullOrWhiteSpace(eventId)) return Results.BadRequest();
            using var json = JsonDocument.Parse(payload);
            var data = json.RootElement.GetProperty("data");
            var shipmentId = data.GetProperty("shipmentId").GetString();
            var status = data.TryGetProperty("canonicalStatus", out var value) ? value.GetString() : "in_transit";
            if (await db.PaymentEvents.AnyAsync(x => x.Provider == "kargojet" && x.EventKey == eventId, ct)) return Results.Accepted();
            var order = await db.Orders.FirstOrDefaultAsync(x => x.ShipmentId == shipmentId, ct);
            if (order is null) return Results.NotFound();
            order.ShippingStatus = status ?? "in_transit";
            if (status == "delivered") order.Status = "Delivered";
            db.PaymentEvents.Add(new PaymentEvent { Provider = "kargojet", EventKey = eventId, OrderNumber = order.Number });
            await db.SaveChangesAsync(ct);
            return Results.Accepted();
        }).RequireRateLimiting("provider-callback");

        app.MapGet("/api/orders/mine", async (ClaimsPrincipal principal, UserManager<CustomerUser> users, AppDbContext db, CancellationToken ct) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var orders = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.UserId == user.Id).OrderByDescending(x => x.Id)
                .Select(x => new { x.Number, x.Status, x.PaymentStatus, x.ShippingStatus, x.TrackingNumber, x.Total, x.CreatedAt, items = x.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice }) }).ToListAsync(ct);
            return Results.Ok(orders);
        }).RequireAuthorization();
        app.MapGet("/api/orders/{number}", async (string number, string? email, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Number == number, ct);
            if (order is null) return Results.NotFound();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (order.UserId is not null ? order.UserId != userId : !string.Equals(order.CustomerEmail, email, StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
            return Results.Ok(new { order.Number, order.Status, order.PaymentStatus, order.ShippingStatus, order.TrackingNumber, order.Total, order.CreatedAt, order.CustomerName, items = order.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice, i.ImageUrl }) });
        });

        app.MapGet("/api/wishlist", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return Results.Ok(await db.Wishlist.Where(x => x.UserId == userId).Join(db.Products, x => x.ProductId, p => p.Id, (x, p) => new { p.Id, p.Name, p.Slug, p.Price, p.ImageTone, p.ImageUrl }).ToListAsync(ct));
        }).RequireAuthorization();
        app.MapPost("/api/wishlist/{productId:int}", async (int productId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.Products.AnyAsync(x => x.Id == productId && x.IsActive, ct)) return Results.NotFound();
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!await db.Wishlist.AnyAsync(x => x.UserId == userId && x.ProductId == productId, ct)) db.Wishlist.Add(new WishlistItem { UserId = userId, ProductId = productId });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapDelete("/api/wishlist/{productId:int}", async (int productId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await db.Wishlist.Where(x => x.UserId == userId && x.ProductId == productId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/account/addresses", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return Results.Ok(await db.CustomerAddresses.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Label, x.RecipientName, x.Phone, x.AddressLine, x.City, x.District, x.PostalCode, x.IsDefault }).ToListAsync(ct));
        }).RequireAuthorization();
        app.MapPost("/api/account/addresses", async (AddressUpsertRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (string.IsNullOrWhiteSpace(request.AddressLine) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.District) || string.IsNullOrWhiteSpace(request.Phone))
                return Results.BadRequest(new { error = "Adres bilgilerini kontrol edin." });
            var address = new CustomerAddress { UserId = userId, Label = string.IsNullOrWhiteSpace(request.Label) ? "Ev" : request.Label.Trim(), RecipientName = request.RecipientName.Trim(), Phone = request.Phone.Trim(), AddressLine = request.AddressLine.Trim(), City = request.City.Trim(), District = request.District.Trim(), PostalCode = request.PostalCode?.Trim() ?? "", IsDefault = request.IsDefault };
            if (address.IsDefault) await db.CustomerAddresses.Where(x => x.UserId == userId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), ct);
            db.CustomerAddresses.Add(address); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/account/addresses/{address.Id}", new { address.Id });
        }).RequireAuthorization();
        app.MapDelete("/api/account/addresses/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var deleted = await db.CustomerAddresses.Where(x => x.Id == id && x.UserId == userId).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        }).RequireAuthorization();

        var admin = app.MapGroup("/api/admin").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapGet("/summary", async (AppDbContext db, CancellationToken ct) => Results.Ok(new
        {
            products = await db.Products.CountAsync(ct),
            lowStock = await db.Products.CountAsync(x => x.Stock < 5 && x.IsActive, ct),
            pendingOrders = await db.Orders.CountAsync(x => x.PaymentStatus == "Paid" && x.ShippingStatus == "NotCreated", ct),
            revenue = await db.Orders.Where(x => x.PaymentStatus == "Paid").SumAsync(x => (decimal?)x.Total, ct) ?? 0
        }));
        admin.MapGet("/products", async (AppDbContext db, CancellationToken ct) => Results.Ok(await db.Products.AsNoTracking().Include(x => x.Category).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.SKU, x.Description, x.Price, x.CompareAtPrice, x.Stock, x.CategoryId, category = x.Category!.Name, x.ImageUrl, x.ImageTone, x.WeightKg, x.WidthCm, x.LengthCm, x.HeightCm, x.IsFeatured, x.IsActive })
            .ToListAsync(ct)));
        admin.MapPost("/products", async (ProductUpsertRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SKU) || request.Price < 0 || request.Stock < 0 || !await db.Categories.AnyAsync(x => x.Id == request.CategoryId, ct)) return Results.BadRequest(new { error = "Ürün bilgilerini kontrol edin." });
            var slug = Slugify(request.Name);
            if (await db.Products.AnyAsync(x => x.Slug == slug || x.SKU == request.SKU, ct)) return Results.Conflict(new { error = "Ürün adı veya SKU zaten kullanılıyor." });
            var product = new Product { Name = request.Name.Trim(), Slug = slug, SKU = request.SKU.Trim(), Description = request.Description.Trim(), Price = request.Price, CompareAtPrice = request.CompareAtPrice, Stock = request.Stock, CategoryId = request.CategoryId, ImageUrl = request.ImageUrl, ImageTone = request.ImageTone, WeightKg = request.WeightKg, WidthCm = request.WidthCm, LengthCm = request.LengthCm, HeightCm = request.HeightCm, IsFeatured = request.IsFeatured, IsActive = request.IsActive };
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/products/{product.Slug}", new { product.Id, product.Slug });
        });
        admin.MapPut("/products/{id:int}", async (int id, ProductUpsertRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null) return Results.NotFound();
            if (request.Price < 0 || request.Stock < 0) return Results.BadRequest(new { error = "Fiyat ve stok negatif olamaz." });
            product.Name = request.Name.Trim(); product.Slug = Slugify(request.Name); product.SKU = request.SKU.Trim(); product.Description = request.Description.Trim(); product.Price = request.Price; product.CompareAtPrice = request.CompareAtPrice; product.Stock = request.Stock; product.CategoryId = request.CategoryId; product.ImageUrl = request.ImageUrl; product.ImageTone = request.ImageTone; product.WeightKg = request.WeightKg; product.WidthCm = request.WidthCm; product.LengthCm = request.LengthCm; product.HeightCm = request.HeightCm; product.IsFeatured = request.IsFeatured; product.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        admin.MapDelete("/products/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null) return Results.NotFound();
            product.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        admin.MapPost("/categories", async (CategoryUpsertRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var slug = Slugify(request.Name);
            if (string.IsNullOrWhiteSpace(request.Name) || await db.Categories.AnyAsync(x => x.Slug == slug, ct)) return Results.Conflict(new { error = "Kategori adı geçersiz veya kullanımda." });
            var category = new Category { Name = request.Name.Trim(), Slug = slug, Description = request.Description.Trim(), Accent = request.Accent };
            db.Categories.Add(category); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/categories/{category.Id}", new { category.Id });
        });
        admin.MapGet("/orders", async (AppDbContext db, string? status, CancellationToken ct) =>
        {
            var query = db.Orders.AsNoTracking().Include(x => x.Items).OrderByDescending(x => x.Id).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
            return Results.Ok(await query.Take(200).Select(x => new { x.Number, x.CustomerName, x.CustomerEmail, x.Status, x.PaymentStatus, x.ShippingStatus, x.TrackingNumber, x.Total, x.CreatedAt, items = x.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice }) }).ToListAsync(ct));
        });
        admin.MapPut("/orders/{number}/status", async (string number, OrderStatusRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var allowed = new[] { "Processing", "Packed", "Shipped", "Delivered", "Cancelled" };
            if (!allowed.Contains(request.Status)) return Results.BadRequest(new { error = "Sipariş durumu geçersiz." });
            var order = await db.Orders.FirstOrDefaultAsync(x => x.Number == number, ct);
            if (order is null) return Results.NotFound();
            if (request.Status is "Shipped" or "Delivered" && order.PaymentStatus != "Paid") return Results.Conflict(new { error = "Ödenmeyen sipariş gönderilemez." });
            if (request.Status == "Cancelled" && order.PaymentStatus == "Paid") return Results.Conflict(new { error = "Ödemesi alınmış sipariş, ödeme sağlayıcısından iade edilmeden iptal edilemez." });
            if (request.Status == "Cancelled" && order.PaymentStatus == "Pending")
            {
                order.PaymentStatus = "Failed";
                var lines = await db.OrderItems.Where(x => x.OrderId == order.Id).ToListAsync(ct);
                foreach (var line in lines)
                {
                    var product = await db.Products.FindAsync([line.ProductId], ct);
                    if (product is not null) product.Stock += line.Quantity;
                }
                if (!string.IsNullOrWhiteSpace(order.CouponCode))
                {
                    var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code == order.CouponCode, ct);
                    if (coupon is not null && coupon.Uses > 0) coupon.Uses--;
                }
            }
            order.Status = request.Status;
            if (request.Status == "Shipped") order.ShippingStatus = "in_transit";
            if (request.Status == "Delivered") order.ShippingStatus = "delivered";
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        admin.MapPost("/orders/{number}/shipment", async (string number, AppDbContext db, IShippingProvider shipping, CancellationToken ct) =>
        {
            var order = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Number == number, ct);
            if (order is null) return Results.NotFound();
            if (order.PaymentStatus != "Paid") return Results.Conflict(new { error = "Ödemesi alınmış siparişler için kargo oluşturulabilir." });
            if (!string.IsNullOrWhiteSpace(order.ShipmentId)) return Results.Ok(new { order.ShipmentId, order.TrackingNumber, order.ShippingStatus });
            try
            {
                var result = await shipping.CreateAsync(order, ct);
                order.ShipmentId = result.ShipmentId;
                order.TrackingNumber = result.TrackingNumber;
                order.ShippingStatus = result.Status;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { order.ShipmentId, order.TrackingNumber, order.ShippingStatus });
            }
            catch { return Results.Problem("Kargo oluşturulamadı. Biraz sonra yeniden deneyin.", statusCode: 502); }
        });
        admin.MapPost("/orders/{number}/confirmation-email", async (string number, AppDbContext db, IEmailSender email, CancellationToken ct) =>
        {
            var order = await db.Orders.FirstOrDefaultAsync(x => x.Number == number, ct);
            if (order is null) return Results.NotFound();
            try { await email.SendOrderConfirmationAsync(order, ct); return Results.NoContent(); }
            catch { return Results.Problem("Sipariş e-postası gönderilemedi.", statusCode: 502); }
        });
        admin.MapGet("/coupons", async (AppDbContext db, CancellationToken ct) => Results.Ok(await db.Coupons.OrderBy(x => x.Code).ToListAsync(ct)));
        admin.MapPost("/coupons", async (CouponUpsertRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || request.PercentOff is <= 0 or >= 100) return Results.BadRequest(new { error = "İndirim kodunu ve yüzde değerini kontrol edin." });
            var code = request.Code.Trim().ToUpperInvariant();
            if (await db.Coupons.AnyAsync(x => x.Code == code, ct)) return Results.Conflict(new { error = "Kod zaten var." });
            var coupon = new Coupon { Code = code, PercentOff = request.PercentOff, MinimumSubtotal = request.MinimumSubtotal, ExpiresAt = request.ExpiresAt, MaximumUses = request.MaximumUses, IsActive = request.IsActive };
            db.Coupons.Add(coupon); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/coupons/{coupon.Id}", new { coupon.Id });
        });
    }

    private static bool IsEmail(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(value, out _);

    private static string Slugify(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        var slug = string.Concat(normalized.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}

public sealed record CouponCheckRequest(string Code, decimal Subtotal);
public sealed record AddressUpsertRequest(string Label, string RecipientName, string Phone, string AddressLine, string City, string District, string? PostalCode, bool IsDefault);
