using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace HotelBookingSaaS.API.Services;

/// <summary>
/// AES-256-CBC encryption for storing sensitive credentials in the database.
/// The encryption key is stored in configuration (use Azure Key Vault or env var in production).
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private const string Prefix = "ENC:";

    public EncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is not configured.");

        // Derive a 32-byte key from the config string using SHA-256
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV so we can decrypt later: [IV (16 bytes)][ciphertext]
        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        // Handle un-encrypted legacy values
        if (!cipherText.StartsWith(Prefix)) return cipherText;

        var combined = Convert.FromBase64String(cipherText[Prefix.Length..]);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // First 16 bytes = IV
        var iv = new byte[16];
        var cipherBytes = new byte[combined.Length - 16];
        Buffer.BlockCopy(combined, 0, iv, 0, 16);
        Buffer.BlockCopy(combined, 16, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool IsEncrypted(string value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix);
}
