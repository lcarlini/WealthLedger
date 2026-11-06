using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace WealthLedger.Infrastructure.Settings;

/// <summary>
/// Encrypts secrets for storage. Windows: DPAPI bound to the current user. Other platforms: AES-GCM with a random
/// key kept in a separate owner-only file next to the settings.
/// </summary>
internal sealed class SecretProtector(string keyFilePath)
{
    private const string DpapiPrefix = "dpapi:";
    private const string AesPrefix = "aes:";

    public string Protect(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        if (OperatingSystem.IsWindows())
            return DpapiPrefix + Convert.ToBase64String(ProtectDpapi(data));

        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(LoadOrCreateKey(), 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return AesPrefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    /// <summary>Returns null when the value cannot be decrypted (other user/machine, missing key, corruption).</summary>
    public string? Unprotect(string stored)
    {
        try
        {
            if (stored.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                if (!OperatingSystem.IsWindows()) return null;
                var raw = Convert.FromBase64String(stored[DpapiPrefix.Length..]);
                return Encoding.UTF8.GetString(UnprotectDpapi(raw));
            }

            if (stored.StartsWith(AesPrefix, StringComparison.Ordinal))
            {
                var raw = Convert.FromBase64String(stored[AesPrefix.Length..]);
                if (raw.Length < 28 || !File.Exists(keyFilePath)) return null;
                var nonce = raw[..12];
                var tag = raw[12..28];
                var cipher = raw[28..];
                var plain = new byte[cipher.Length];
                using var aes = new AesGcm(File.ReadAllBytes(keyFilePath), 16);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or IOException)
        {
            return null;
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ProtectDpapi(byte[] data)
        => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] UnprotectDpapi(byte[] data)
        => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);

    private byte[] LoadOrCreateKey()
    {
        if (File.Exists(keyFilePath))
            return File.ReadAllBytes(keyFilePath);

        var key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)!);
        File.WriteAllBytes(keyFilePath, key);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return key;
    }
}
