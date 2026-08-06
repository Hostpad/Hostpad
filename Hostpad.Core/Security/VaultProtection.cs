namespace Hostpad.Core.Security;

/// <summary>
/// How the data key should be protected. At least one mechanism is required —
/// an unprotected vault is not an option Hostpad offers.
/// </summary>
public sealed class VaultProtection
{
    /// <summary>Bind the vault to the current Windows account.</summary>
    public bool UseDpapi { get; init; }

    /// <summary>Master password, or null for none.</summary>
    public string? Password { get; init; }

    public int Iterations { get; init; } = PasswordKeyDerivation.DefaultIterations;

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    /// <summary>Default for a new vault: no password prompt, but tied to the Windows account.</summary>
    public static VaultProtection DpapiOnly => new() { UseDpapi = true };

    /// <summary>Password required at startup, and still tied to the Windows account.</summary>
    public static VaultProtection WithPassword(string password) =>
        new() { UseDpapi = true, Password = password };

    /// <summary>Password only — the vault stays readable after a Windows reinstall or on another machine.</summary>
    public static VaultProtection PasswordOnly(string password) =>
        new() { UseDpapi = false, Password = password };

    internal void Validate()
    {
        if (!UseDpapi && !HasPassword)
        {
            throw new ArgumentException(
                "A vault needs at least one protection mechanism: DPAPI, a password, or both.");
        }
    }
}
