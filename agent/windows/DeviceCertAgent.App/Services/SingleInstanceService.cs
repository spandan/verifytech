using System.IO.Pipes;
using System.Text;
using System.Windows;

namespace DeviceCertAgent.App.Services;

/// <summary>
/// Ensures one agent window; forwards deep-link args to the running instance.
/// </summary>
public static class SingleInstanceService
{
    public const string MutexName = "Certronx.DeviceCertAgent.SingleInstance";
    public const string PipeName = "Certronx.DeviceCertAgent.Pipe";
    private static Mutex? _mutex;

    public static bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            ForwardArgs(args);
            return false;
        }

        _ = Task.Run(ListenForForwardedArgs);
        return true;
    }

    private static void ForwardArgs(string[] args)
    {
        if (args.Length == 0)
            return;

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(string.Join('\n', args));
        }
        catch
        {
            // Running instance may be busy; user can paste pairing code manually.
        }
    }

    private static async Task ListenForForwardedArgs()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var payload = await reader.ReadToEndAsync();
                var args = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (args.Length == 0)
                    continue;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow?.DataContext is ViewModels.ShellViewModel shell)
                    {
                        Application.Current.MainWindow.Activate();
                        if (DeviceCertAgent.Core.Configuration.SecureEndpointResolver.TryParseDeepLink(args[0], out var code))
                        {
                            shell.HandleForwardedPairingCode(code);
                        }
                    }
                });
            }
            catch
            {
                await Task.Delay(250);
            }
        }
    }
}
