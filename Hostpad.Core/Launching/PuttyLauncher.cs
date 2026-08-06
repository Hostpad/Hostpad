using Hostpad.Core.Model;

namespace Hostpad.Core.Launching;

public interface ILauncher
{
    bool Handles(Protocol protocol);

    LaunchPlan CreatePlan(LaunchRequest request);
}

/// <summary>Builds a PuTTY command line for an SSH session.</summary>
public sealed class PuttyLauncher : ILauncher
{
    public bool Handles(Protocol protocol) => protocol is Protocol.Ssh;

    public LaunchPlan CreatePlan(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = request.Settings.Putty;
        var connection = request.Connection;
        var arguments = new List<string> { "-ssh" };

        if (connection.Jump is { } jump)
        {
            // PuTTY tunnels through plink acting as a proxy; %host and %port are
            // PuTTY's own placeholders for the eventual target.
            var jumpTarget = jump.Username is null ? jump.Host : $"{jump.Username}@{jump.Host}";
            arguments.Add("-proxycmd");
            arguments.Add($"plink -batch -P {jump.EffectivePort} {jumpTarget} -nc %host:%port");
        }

        arguments.Add("-P");
        arguments.Add(request.Port.ToString());

        if (!string.IsNullOrEmpty(connection.Credential.Username))
        {
            arguments.Add("-l");
            arguments.Add(connection.Credential.Username);
        }

        if (connection.Credential.HasPassword)
        {
            // -pw puts the password on the command line, where any user on the
            // machine can read it from the process list for as long as PuTTY
            // runs. It is what AutoPuTTY did and what PuTTY offers; key files
            // are the way to avoid it.
            arguments.Add("-pw");
            arguments.Add(connection.Credential.Password!);
        }

        if (settings.UseKeyFile && !string.IsNullOrWhiteSpace(settings.KeyFilePath))
        {
            arguments.Add("-i");
            arguments.Add(settings.KeyFilePath);
        }

        if (settings.X11Forwarding)
        {
            arguments.Add("-X");
        }

        string? commandFile = null;
        if (settings.ExecuteCommands && !string.IsNullOrWhiteSpace(settings.Command))
        {
            commandFile = TemporaryFiles.Write("hostpad-cmd", ".txt", settings.Command);
            arguments.Add("-m");
            arguments.Add(commandFile);
            // Without -t the session closes as soon as the command finishes.
            arguments.Add("-t");
        }

        arguments.Add(connection.Host);

        return new LaunchPlan
        {
            FileName = settings.Path,
            Arguments = arguments,
            TemporaryFile = commandFile,
        };
    }
}
