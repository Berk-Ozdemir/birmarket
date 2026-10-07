namespace Birmarket.Api.Configuration;

public static class EnvironmentFile
{
    public static void Load()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? exampleFile = null;
        while (directory is not null)
        {
            var file = Path.Combine(directory.FullName, "birmarket.env");
            if (File.Exists(file))
            {
                LoadFile(file);
                return;
            }
            var example = Path.Combine(directory.FullName, "birmarket.env.example");
            if (exampleFile is null && File.Exists(example)) exampleFile = example;
            directory = directory.Parent;
        }

        if (exampleFile is null) return;
        var generatedEnvironmentFile = Path.Combine(Path.GetDirectoryName(exampleFile)!, "birmarket.env");
        File.Copy(exampleFile, generatedEnvironmentFile);
        LoadFile(generatedEnvironmentFile);
    }

    private static void LoadFile(string file)
    {
        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('=')) continue;
            var separator = line.IndexOf('=');
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            if (key.Length > 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}

public sealed class AppRuntimeOptions
{
    public bool DemoMode { get; init; }
    public string DatabasePath { get; init; } = string.Empty;
    public string DemoDatabasePath { get; init; } = string.Empty;
    public string RealDatabasePath { get; init; } = string.Empty;
    public string PaymentEnvironment { get; init; } = "sandbox";
    public string PublicBaseUrl { get; init; } = "";
    public string AdminEmail { get; init; } = "";
    public string AdminPassword { get; init; } = "";
    public string DataProtectionKey { get; init; } = "";
    public string IyzicoApiKey { get; init; } = "";
    public string IyzicoSecretKey { get; init; } = "";
    public string PayTrMerchantId { get; init; } = "";
    public string PayTrMerchantKey { get; init; } = "";
    public string PayTrMerchantSalt { get; init; } = "";
    public string KargoJetToken { get; init; } = "";
    public string KargoJetWebhookSecret { get; init; } = "";
    public string KargoJetCarrierCode { get; init; } = "aras";
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUsername { get; init; } = "";
    public string SmtpPassword { get; init; } = "";
    public string EmailFrom { get; init; } = "";
    public string EmailFromName { get; init; } = "Birmarket";
    public decimal FlatShippingFee { get; init; } = 49;
    public decimal FreeShippingThreshold { get; init; } = 1500;

    public static AppRuntimeOptions From(IConfiguration configuration)
    {
        var demo = bool.TryParse(configuration["birmarket_demo_mode"], out var configured) ? configured : true;
        var root = FindProjectRoot();
        var demoDatabasePath = configuration["birmarket_demo_sqlite_path"] ?? ".data/birmarket-demo.sqlite3";
        var realDatabasePath = configuration["birmarket_real_sqlite_path"] ?? ".data/birmarket.sqlite3";
        if (!Path.IsPathRooted(demoDatabasePath)) demoDatabasePath = Path.Combine(root, demoDatabasePath);
        if (!Path.IsPathRooted(realDatabasePath)) realDatabasePath = Path.Combine(root, realDatabasePath);
        demoDatabasePath = Path.GetFullPath(demoDatabasePath);
        realDatabasePath = Path.GetFullPath(realDatabasePath);
        var databasePath = demo ? demoDatabasePath : realDatabasePath;
        var dataProtectionKey = configuration["birmarket_data_protection_key"] ?? "";
        if (string.IsNullOrWhiteSpace(dataProtectionKey) && demo)
        {
            var keyFile = Path.Combine(Path.GetDirectoryName(demoDatabasePath)!, "demo-data-protection.key");
            Directory.CreateDirectory(Path.GetDirectoryName(keyFile)!);
            dataProtectionKey = File.Exists(keyFile) ? File.ReadAllText(keyFile).Trim() : Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            if (!File.Exists(keyFile)) File.WriteAllText(keyFile, dataProtectionKey);
        }
        if (!string.IsNullOrWhiteSpace(dataProtectionKey)) Environment.SetEnvironmentVariable("birmarket_data_protection_key", dataProtectionKey);
        return new AppRuntimeOptions
        {
            DemoMode = demo,
            DatabasePath = databasePath,
            DemoDatabasePath = demoDatabasePath,
            RealDatabasePath = realDatabasePath,
            PaymentEnvironment = configuration["birmarket_provider_environment"] ?? "sandbox",
            PublicBaseUrl = configuration["birmarket_public_base_url"] ?? (demo ? "http://localhost:4200" : ""),
            AdminEmail = configuration["birmarket_admin_email"] ?? "",
            AdminPassword = configuration["birmarket_admin_password"] ?? "",
            DataProtectionKey = dataProtectionKey,
            IyzicoApiKey = configuration["iyzico_api_key"] ?? "",
            IyzicoSecretKey = configuration["iyzico_secret_key"] ?? "",
            PayTrMerchantId = configuration["paytr_merchant_id"] ?? "",
            PayTrMerchantKey = configuration["paytr_merchant_key"] ?? "",
            PayTrMerchantSalt = configuration["paytr_merchant_salt"] ?? "",
            KargoJetToken = configuration["kargojet_api_token"] ?? "",
            KargoJetWebhookSecret = configuration["kargojet_webhook_secret"] ?? "",
            KargoJetCarrierCode = configuration["kargojet_carrier_code"] ?? "aras",
            SmtpHost = configuration["smtp_host"] ?? "",
            SmtpPort = int.TryParse(configuration["smtp_port"], out var smtpPort) ? smtpPort : 587,
            SmtpUsername = configuration["smtp_username"] ?? "",
            SmtpPassword = configuration["smtp_password"] ?? "",
            EmailFrom = configuration["email_from"] ?? "",
            EmailFromName = configuration["email_from_name"] ?? "Birmarket",
            FlatShippingFee = decimal.TryParse(configuration["birmarket_flat_shipping_fee"], out var shipping) ? shipping : 49,
            FreeShippingThreshold = decimal.TryParse(configuration["birmarket_free_shipping_threshold"], out var threshold) ? threshold : 1500
        };
    }

    public IReadOnlyList<string> MissingRealSettings()
    {
        var missing = new List<string>();
        if (string.Equals(DemoDatabasePath, RealDatabasePath, StringComparison.OrdinalIgnoreCase))
            missing.Add("birmarket_demo_sqlite_path and birmarket_real_sqlite_path (must be different files)");
        if (DemoMode) return missing;
        var required = new Dictionary<string, string>
        {
            ["birmarket_admin_email"] = AdminEmail,
            ["birmarket_admin_password"] = AdminPassword,
            ["birmarket_data_protection_key"] = DataProtectionKey,
            ["birmarket_public_base_url"] = PublicBaseUrl,
            ["iyzico_api_key"] = IyzicoApiKey,
            ["iyzico_secret_key"] = IyzicoSecretKey,
            ["paytr_merchant_id"] = PayTrMerchantId,
            ["paytr_merchant_key"] = PayTrMerchantKey,
            ["paytr_merchant_salt"] = PayTrMerchantSalt,
            ["kargojet_api_token"] = KargoJetToken,
            ["kargojet_webhook_secret"] = KargoJetWebhookSecret,
            ["smtp_host"] = SmtpHost,
            ["smtp_username"] = SmtpUsername,
            ["smtp_password"] = SmtpPassword,
            ["email_from"] = EmailFrom
        };
        missing.AddRange(required.Where(x => IsMissingSecret(x.Value)).Select(x => x.Key));
        if (DataProtectionKey.Length < 32) missing.Add("birmarket_data_protection_key (at least 32 characters)");
        if (PaymentEnvironment is not ("sandbox" or "live")) missing.Add("birmarket_provider_environment (sandbox|live)");
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            missing.Add("birmarket_public_base_url (absolute HTTPS URL)");
        return missing;
    }

    private static bool IsMissingSecret(string value) =>
        string.IsNullOrWhiteSpace(value) || value.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase);

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "birmarket.env.example")) || Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
