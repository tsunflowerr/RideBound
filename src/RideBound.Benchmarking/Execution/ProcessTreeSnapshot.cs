using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RideBound.Benchmarking.Execution;

internal static class ProcessTreeSnapshot
{
    private const uint SnapshotProcesses = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    public static IReadOnlyList<ProcessInstanceIdentity> GetProcessInstances(
        ProcessInstanceIdentity rootProcess)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [rootProcess];
        }

        var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);

        if (snapshot == InvalidHandleValue)
        {
            throw new InvalidOperationException(
                "Windows process-tree snapshot could not be created.");
        }

        try
        {
            var rawEntries = new List<(int ProcessId, int ParentProcessId)>();
            var entry = new ProcessEntry32
            {
                Size = checked((uint)Marshal.SizeOf<ProcessEntry32>()),
            };

            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    rawEntries.Add(
                        (
                            checked((int)entry.ProcessId),
                            checked((int)entry.ParentProcessId)));
                    entry.Size = checked((uint)Marshal.SizeOf<ProcessEntry32>());
                }
                while (Process32Next(snapshot, ref entry));
            }

            var entries = new List<ProcessSnapshotEntry>();

            foreach (var rawEntry in rawEntries)
            {
                if (rawEntry.ProcessId == rootProcess.ProcessId)
                {
                    entries.Add(
                        new ProcessSnapshotEntry(
                            rawEntry.ProcessId,
                            rawEntry.ParentProcessId,
                            rootProcess.StartTimeUtcTicks));
                    continue;
                }

                try
                {
                    using var process = Process.GetProcessById(rawEntry.ProcessId);
                    entries.Add(
                        new ProcessSnapshotEntry(
                            rawEntry.ProcessId,
                            rawEntry.ParentProcessId,
                            GetStartTimeUtcTicks(process)));
                }
                catch (ArgumentException)
                {
                    // A process may exit between the Toolhelp snapshot and inspection.
                }
                catch (InvalidOperationException)
                {
                    // An exited process cannot contribute to the current tree.
                }
                catch (Win32Exception)
                {
                    // Inaccessible processes cannot be verified as descendants.
                }
            }

            return SelectProcessTree(rootProcess, entries);
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    internal static IReadOnlyList<ProcessInstanceIdentity> SelectProcessTree(
        ProcessInstanceIdentity rootProcess,
        IReadOnlyList<ProcessSnapshotEntry> snapshotEntries)
    {
        ArgumentNullException.ThrowIfNull(snapshotEntries);
        var childrenByParent = snapshotEntries
            .Where(entry => entry.ProcessId != rootProcess.ProcessId)
            .GroupBy(entry => entry.ParentProcessId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var result = new Dictionary<int, ProcessInstanceIdentity>
        {
            [rootProcess.ProcessId] = rootProcess,
        };
        var pending = new Queue<ProcessInstanceIdentity>();
        pending.Enqueue(rootProcess);

        while (pending.Count > 0)
        {
            var parent = pending.Dequeue();

            if (!childrenByParent.TryGetValue(parent.ProcessId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (child.StartTimeUtcTicks < parent.StartTimeUtcTicks
                    || result.ContainsKey(child.ProcessId))
                {
                    continue;
                }

                var identity = new ProcessInstanceIdentity(
                    child.ProcessId,
                    child.StartTimeUtcTicks);
                result.Add(child.ProcessId, identity);
                pending.Enqueue(identity);
            }
        }

        return result.Values
            .OrderBy(identity => identity.ProcessId)
            .ToArray();
    }

    public static ProcessTreeUsage Observe(
        ProcessInstanceIdentity rootProcess,
        IDictionary<ProcessInstanceIdentity, long> maximumCpuByProcess)
    {
        var processInstances = GetProcessInstances(rootProcess);
        long workingSet = 0;

        foreach (var processInstance in processInstances)
        {
            try
            {
                using var process = Process.GetProcessById(processInstance.ProcessId);
                process.Refresh();

                if (GetStartTimeUtcTicks(process) != processInstance.StartTimeUtcTicks)
                {
                    continue;
                }

                workingSet = checked(workingSet + process.WorkingSet64);
                var cpu = checked((long)process.TotalProcessorTime.TotalMilliseconds);

                if (!maximumCpuByProcess.TryGetValue(processInstance, out var previous)
                    || cpu > previous)
                {
                    maximumCpuByProcess[processInstance] = cpu;
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
            catch (Win32Exception)
            {
                // Preserve the last observation for a now-inaccessible process.
            }
        }

        return new ProcessTreeUsage(
            maximumCpuByProcess.Values.Sum(),
            workingSet,
            processInstances.Count);
    }

    internal static long GetStartTimeUtcTicks(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.StartTime.ToUniversalTime().Ticks;
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

    internal readonly record struct ProcessInstanceIdentity(
        int ProcessId,
        long StartTimeUtcTicks);

    internal readonly record struct ProcessSnapshotEntry(
        int ProcessId,
        int ParentProcessId,
        long StartTimeUtcTicks);

    internal sealed record ProcessTreeUsage(
        long CpuTimeMs,
        long WorkingSetBytes,
        long ProcessCount);
}
