using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CorePulse.Core.Services;

public static class MemoryOptimizer
{
    private const int SystemMemoryListInformation = 0x0050;
    private const int MemoryPurgeStandbyList = 4;
    private const int MemoryEmptyWorkingSets = 2;

    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SE_PROFILE_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
    private const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, int cb);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PERFORMANCE_INFORMATION
    {
        public int cb;
        public IntPtr CommitTotal;
        public IntPtr CommitLimit;
        public IntPtr CommitPeak;
        public IntPtr PhysicalTotal;
        public IntPtr PhysicalAvailable;
        public IntPtr SystemCache;
        public IntPtr KernelTotal;
        public IntPtr KernelPaged;
        public IntPtr KernelNonpaged;
        public IntPtr PageSize;
        public int HandleCount;
        public int ProcessCount;
        public int ThreadCount;
    }

    /// <summary>
    /// Uygulamanın Administrator (Yönetici) olarak çalışıp çalışmadığını kontrol eder.
    /// </summary>
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Windows Sistem Standby (Önbelleğe Alınmış) Belleğini ve çalışma kümelerini temizler.
    /// </summary>
    public static (bool Success, string Message, long FreedBytes) PurgeStandbyMemory()
    {
        if (!IsAdministrator())
        {
            return (false, "Standby belleği temizlemek için CorePulse Yönetici olarak çalıştırılmalıdır.", 0);
        }

        long cacheBefore = GetSystemCacheBytes();

        try
        {
            // 1. Windows NT yetkilerini (SeProfileSingleProcessPrivilege ve SeIncreaseQuotaPrivilege) aç
            EnablePrivilege(SE_PROFILE_SINGLE_PROCESS_NAME);
            EnablePrivilege(SE_INCREASE_QUOTA_NAME);

            // 2. Sistem çalışma kümelerini boşalt (tüm arka plan uygulamalarının inaktif sayfalarını standby listesine kaydır)
            int cmdWs = MemoryEmptyWorkingSets;
            NtSetSystemInformation(SystemMemoryListInformation, ref cmdWs, sizeof(int));

            // 3. Standby Listesini (Önbelleğe Alınmış Belleği) tamamen temizle
            int cmdPurge = MemoryPurgeStandbyList;
            int status = NtSetSystemInformation(SystemMemoryListInformation, ref cmdPurge, sizeof(int));

            // 4. Kendi çalışma belleğimizi de boşalt
            try
            {
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }

            long cacheAfter = GetSystemCacheBytes();
            long freed = Math.Max(0, cacheBefore - cacheAfter);

            if (status == 0)
            {
                double freedMb = freed / (1024.0 * 1024.0);
                string text = freedMb >= 1024
                    ? $"Standby bellek temizlendi ({(freedMb / 1024.0):F1} GB önbellek boşaltıldı) ✓"
                    : $"Standby bellek temizlendi ({freedMb:F0} MB önbellek boşaltıldı) ✓";
                return (true, text, freed);
            }
            else
            {
                return (false, $"Standby listesi temizlenemedi (NTSTATUS: 0x{status:X8})", 0);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Bellek optimizasyonu hatası: {ex.Message}", 0);
        }
    }

    public static long GetSystemCacheBytes()
    {
        try
        {
            var perf = new PERFORMANCE_INFORMATION();
            perf.cb = Marshal.SizeOf(perf);
            if (GetPerformanceInfo(out perf, perf.cb))
            {
                return perf.SystemCache.ToInt64() * perf.PageSize.ToInt64();
            }
        }
        catch { }
        return 0;
    }

    private static void EnablePrivilege(string privilegeName)
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out var tokenHandle))
            return;

        var tp = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
            Attributes = (int)SE_PRIVILEGE_ENABLED
        };

        if (!LookupPrivilegeValue(null, privilegeName, out tp.Luid))
            return;

        AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }
}
