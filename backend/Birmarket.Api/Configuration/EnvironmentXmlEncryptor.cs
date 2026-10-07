using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Birmarket.Api.Configuration;

public sealed class EnvironmentXmlEncryptor(string keyMaterial) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(DeriveKey(keyMaterial), tag.Length))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var encrypted = new XElement("encryptedData",
            new XAttribute("nonce", Convert.ToBase64String(nonce)),
            new XAttribute("tag", Convert.ToBase64String(tag)),
            Convert.ToBase64String(ciphertext));
        return new EncryptedXmlInfo(encrypted, typeof(EnvironmentXmlDecryptor));
    }

    internal static byte[] DeriveKey(string material) => SHA256.HashData(Encoding.UTF8.GetBytes(material));
}

public sealed class EnvironmentXmlDecryptor : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var key = Environment.GetEnvironmentVariable("birmarket_data_protection_key");
        if (string.IsNullOrWhiteSpace(key)) throw new CryptographicException("The data protection key is not configured.");
        var nonce = Convert.FromBase64String((string)encryptedElement.Attribute("nonce")!);
        var tag = Convert.FromBase64String((string)encryptedElement.Attribute("tag")!);
        var ciphertext = Convert.FromBase64String(encryptedElement.Value);
        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(EnvironmentXmlEncryptor.DeriveKey(key), tag.Length))
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }
}
