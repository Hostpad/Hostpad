namespace Hostpad.Core.Security;

public class VaultException : Exception
{
    public VaultException(string message) : base(message)
    {
    }

    public VaultException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>The vault needs a master password that was not supplied, or the one supplied is wrong.</summary>
public sealed class VaultAuthenticationException : VaultException
{
    public VaultAuthenticationException(string message) : base(message)
    {
    }

    public VaultAuthenticationException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>The file is not a Hostpad vault, is truncated, or was written by a newer version.</summary>
public sealed class VaultFormatException : VaultException
{
    public VaultFormatException(string message) : base(message)
    {
    }

    public VaultFormatException(string message, Exception inner) : base(message, inner)
    {
    }
}
