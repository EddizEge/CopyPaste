using System.Diagnostics;
using System.Runtime.InteropServices;
using CopyPaste.Core.Models;

namespace CopyPaste.App.Services;

public static class PowerManagementService
{
    public static IDisposable PreventSleep()
    {
        SetThreadExecutionState(ExecutionState.Continuous | ExecutionState.SystemRequired);
        return new SleepGuard();
    }

    public static void Execute(CompletionAction action)
    {
        switch (action)
        {
            case CompletionAction.Sleep:
                if (!SetSuspendState(false, true, false))
                    throw new InvalidOperationException("Windows uyku durumuna geçirilemedi.");
                break;
            case CompletionAction.ShutDown:
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                    Arguments = "/s /t 30 /c \"CopyPaste transferi tamamlandı\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                break;
        }
    }

    private sealed class SleepGuard : IDisposable
    {
        public void Dispose() => SetThreadExecutionState(ExecutionState.Continuous);
    }

    [Flags]
    private enum ExecutionState : uint
    {
        SystemRequired = 0x00000001,
        Continuous = 0x80000000
    }

    [DllImport("kernel32.dll")]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState state);

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
