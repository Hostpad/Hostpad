using System.Text;
using Hostpad.Core.Security;
using Xunit;

namespace Hostpad.Core.Tests;

public class VaultTests
{
    // Real vaults use 600k iterations; tests only need the code path, not the cost.
    private static VaultProtection PasswordOnly(string password) =>
        new() { UseDpapi = false, Password = password, Iterations = 1_000 };

    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("""{"connections":[]}""");

    [Fact]
    public void Seal_ThenOpen_RoundTripsThePayload()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("correct horse"));

        Assert.Equal(Payload, Vault.Open(envelope, "correct horse"));
    }

    [Fact]
    public void Seal_DoesNotLeaveThePlaintextInTheEnvelope()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("pw"));

        Assert.NotEqual(Payload, envelope.Payload.Ciphertext);
        Assert.DoesNotContain("connections", Encoding.UTF8.GetString(envelope.Payload.Ciphertext));
    }

    [Fact]
    public void Open_RejectsTheWrongPassword()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("right"));

        Assert.Throws<VaultAuthenticationException>(() => Vault.Open(envelope, "wrong"));
    }

    [Fact]
    public void Open_RequiresAPasswordWhenTheVaultHasOne()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("pw"));

        Assert.Throws<VaultAuthenticationException>(() => Vault.Open(envelope, password: null));
    }

    [Fact]
    public void Open_DetectsATamperedPayload()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("pw"));
        envelope.Payload.Ciphertext[0] ^= 0xFF;

        Assert.Throws<VaultFormatException>(() => Vault.Open(envelope, "pw"));
    }

    [Fact]
    public void Open_RejectsAFormatFromTheFuture()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("pw"));
        envelope.FormatVersion = VaultEnvelope.CurrentFormatVersion + 1;

        var error = Assert.Throws<VaultFormatException>(() => Vault.Open(envelope, "pw"));
        Assert.Contains("Update Hostpad", error.Message);
    }

    [Fact]
    public void Seal_RefusesAVaultWithNoProtectionAtAll()
    {
        Assert.Throws<ArgumentException>(
            () => Vault.Seal(Payload, new VaultProtection { UseDpapi = false, Password = null }));
    }

    [Fact]
    public void Rewrap_ChangesThePasswordWithoutTouchingTheCiphertext()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("old"));
        var originalCiphertext = envelope.Payload.Ciphertext;

        var rewrapped = Vault.Rewrap(envelope, "old", PasswordOnly("new"));

        Assert.Equal(originalCiphertext, rewrapped.Payload.Ciphertext);
        Assert.Equal(Payload, Vault.Open(rewrapped, "new"));
        Assert.Throws<VaultAuthenticationException>(() => Vault.Open(rewrapped, "old"));
    }

    [Fact]
    public void Rewrap_RejectsTheWrongCurrentPassword()
    {
        var envelope = Vault.Seal(Payload, PasswordOnly("old"));

        Assert.Throws<VaultAuthenticationException>(
            () => Vault.Rewrap(envelope, "not-the-old-one", PasswordOnly("new")));
    }

    [Fact]
    public void DpapiOnlyVault_OpensWithoutAPassword()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var envelope = Vault.Seal(Payload, VaultProtection.DpapiOnly);

        Assert.False(envelope.HasPassword);
        Assert.Equal(Payload, Vault.Open(envelope));
    }

    [Fact]
    public void DualProtectedVault_OpensViaDpapiAndViaThePassword()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protection = new VaultProtection { UseDpapi = true, Password = "pw", Iterations = 1_000 };
        var envelope = Vault.Seal(Payload, protection);

        // DPAPI path: no password needed on this account.
        Assert.Equal(Payload, Vault.Open(envelope));

        // Password path: still works once DPAPI is out of the picture.
        envelope.DpapiWrappedKey = null;
        Assert.Equal(Payload, Vault.Open(envelope, "pw"));
    }
}
