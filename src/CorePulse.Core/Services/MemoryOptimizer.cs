using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CorePulse.Core.Services;

public static class MemoryOptimizer
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    public static long OptimizeMemory()
    {
        long memoryBefore = GC.GetTotalMemory(false);

        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var currentProc = Process.GetCurrentProcess();
            EmptyWorkingSet(currentProc.Handle);
            SetProcessWorkingSetSize(currentProc.Handle, -1, -1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MemoryOptimizer] Hata: {ex.Message}");
        }

        long memoryAfter = GC.GetTotalMemory(false);
        return Math.Max(0, memoryBefore - memoryAfter);
    }
}
