using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RideBound.Benchmarking.Execution;

internal static class ProcessTreeSnapshot
{
    private const uint SnapshotProcesses = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    public static IReadOnlyList<int> GetProcessIds(int rootProcessId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [rootProcessId];
        }

        var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);

        if (snapshot == InvalidHandleValue)
        {
            throw new InvalidOperationException(
                "Windows process-tree snapshot could not be created.");
        }

        try
        {
            var parentByProcess = new Dictionary<int, int>();
            var entry = new ProcessEntry32
            {
                Size = checked((uint)Marshal.SizeOf<ProcessEntry32>()),
            };

            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    parentByProcess[checked((int)entry.ProcessId)] =
                        checked((int)entry.ParentProcessId);
                    entry.Size = checked((uint)Marshal.SizeOf<ProcessEntry32>());
                }
                while (Process32Next(snapshot, ref entry));
            }

            var result = new HashSet<int> { rootProcessId };
            var added = true;

            while (added)
            {
                added = false;

                foreach (var pair in parentByProcess)
                {
                    if (result.Contains(pair.Value) && result.Add(pair.Key))
                    {
                        added = true;
                    }
                }
            }

            return result.Order().ToArray();
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    public static ProcessTreeUsage Observe(
        int rootProcessId,
        IDictionary<int, long> maximumCpuByProcess)
    {
        var processIds = GetProcessIds(rootProcessId);
        long workingSet = 0;

        foreach (var processId in processIds)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Refresh();
                workingSet = checked(workingSet + process.WorkingSet64);
                var cpu = checked((long)process.TotalProcessorTime.TotalMilliseconds);

                if (!maximumCpuByProcess.TryGetValue(processId, out var previous)
                    || cpu > previous)
                {
                    maximumCpuByProcess[processId] = cpu;
                }
            }
            catch (ArgumentException)
            {
                // A process may exit between the tree snapshot and observation.
            }
            catch (InvalidOperationException)
            {
                // Preserve the last observation for an exited process.
            }
        }

        return new ProcessTreeUsage(
            maximumCpuByProcess.Values.Sum(),
            workingSet,
            processIds.Count);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    internal sealed record ProcessTreeUsage(
        long CpuTimeMs,
        long WorkingSetBytes,
        long ProcessCount);
}
