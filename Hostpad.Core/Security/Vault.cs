using System.Security.Cryptography;

namespace Hostpad.Core.Security;

/// <summary>
/// Seals and opens vault envelopes. Works on bytes only — what those bytes mean
/// is the storage layer's business.
/// </summary>
public static class Vault
{
    public static VaultEnvelope Seal(ReadOnlySpan<byte> plaintext, VaultProtection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);
        protection.Validate();

        var dataKey = AesGcmCipher.NewKey();
        try
        {
            var envelope = new VaultEnvelope { Payload = AesGcmCipher.Encrypt(dataKey, plaintext) };
            WrapDataKey(envelope, dataKey, protection);
            return envelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <param name="password">Required when the envelope carries password protection and DPAPI is unavailable.</param>
    public static byte[] Open(VaultEnvelope envelope, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        VerifyFormat(envelope);

        var dataKey = UnwrapDataKey(envelope, password);
        try
        {
            return AesGcmCipher.Decrypt(dataKey, envelope.Payload);
        }
        catch (CryptographicException ex)
        {
            throw new VaultFormatException("The vault payload failed its integrity check.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>
    /// Changes how an existing vault is protected — set, change or clear the
    /// master password — without re-encrypting the document.
    /// </summary>
    public static VaultEnvelope Rewrap(
        VaultEnvelope envelope,
        string? currentPassword,
        VaultProtection newProtection)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(newProtection);
        VerifyFormat(envelope);
        newProtection.Validate();

        var dataKey = UnwrapDataKey(envelope, currentPassword);
        try
        {
            var rewrapped = new VaultEnvelope { Payload = envelope.Payload };
            WrapDataKey(rewrapped, dataKey, newProtection);
            return rewrapped;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static void WrapDataKey(VaultEnvelope envelope, byte[] dataKey, VaultProtection protection)
    {
        if (protection.UseDpapi)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI protection requires Windows.");
            }

            envelope.DpapiWrappedKey = DpapiKeyProtector.Protect(dataKey);
        }

        if (!protection.HasPassword)
        {
            return;
        }

        var salt = PasswordKeyDerivation.NewSalt();
        var passwordKey = PasswordKeyDerivation.DeriveKey(protection.Password!, salt, protection.Iterations);
        try
        {
            envelope.Kdf = new KdfParameters { Salt = salt, Iterations = protection.Iterations };
            envelope.PasswordWrappedKey = AesGcmCipher.Encrypt(passwordKey, dataKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordKey);
        }
    }

    /// <summary>
    /// Recovers the data key. DPAPI is tried first so the common case — a vault
    /// on its own machine — never asks for a password it does not need.
    /// </summary>
    private static byte[] UnwrapDataKey(VaultEnvelope envelope, string? password)
    {
        if (envelope is { HasDpapi: true } && OperatingSystem.IsWindows())
        {
            try
            {
                return DpapiKeyProtector.Unprotect(envelope.DpapiWrappedKey!);
            }
            catch (CryptographicException) when (envelope.HasPassword)
            {
                // Different Windows account: fall through to the password.
            }
            catch (CryptographicException ex)
            {
                throw new VaultAuthenticationException(
                    "This vault is bound to a different Windows account and has no master password.", ex);
            }
        }

        if (!envelope.HasPassword)
        {
            throw new VaultAuthenticationException("This vault cannot be opened on this machine.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new VaultAuthenticationException("This vault requires a master password.");
        }

        var passwordKey = PasswordKeyDerivation.DeriveKey(
            password,
            envelope.Kdf!.Salt,
            envelope.Kdf.Iterations);
        try
        {
            return AesGcmCipher.Decrypt(passwordKey, envelope.PasswordWrappedKey!);
        }
        catch (CryptographicException ex)
        {
            throw new VaultAuthenticationException("Incorrect master password.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordKey);
        }
    }

    private static void VerifyFormat(VaultEnvelope envelope)
    {
        if (envelope.Magic != VaultEnvelope.ExpectedMagic)
        {
            throw new VaultFormatException("This file is not a Hostpad vault.");
        }

        if (envelope.FormatVersion > VaultEnvelope.CurrentFormatVersion)
        {
            throw new VaultFormatException(
                $"This vault uses format version {envelope.FormatVersion}; " +
                $"this build understands up to {VaultEnvelope.CurrentFormatVersion}. Update Hostpad.");
        }
    }
}
