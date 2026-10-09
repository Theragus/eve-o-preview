//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using EveOPreview.Configuration;
using EveOPreview.Helper;
using EveOPreview.Services.Interface;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using static EveOPreview.Services.Interop.KernelNativeMethods;
using static System.Net.Mime.MediaTypeNames;

namespace EveOPreview.Services.Implementation;

public class CpuAffinityService : ICpuAffinityService
{
    private readonly ILogger _logger;
    private readonly IThumbnailConfiguration _config;
    private const int MaxBitsPerGroup = 64;
    private const ulong SingleCoreBit = 1UL;
    public List<int> PCores { get; } = [];
    public List<int> ECores { get; } = [];

    private IntPtr _activeMask; // Where we will place the current active client.
    private IntPtr _nextMask; // Where we will place the predicted next active client.
    private IntPtr _prevMask; // Where we will place the previous client (ready to go back quickly if they reverse the expected order)
    private IntPtr _backgroundMask; // Where all background processes will run otherwise.
    private IntPtr _allCoresMask; // List of all cores so we can easily turn our automation back off.

    private readonly HashSet<(int Pid, IntPtr Handle)> _currentBackgroundHandles = [];
    private readonly Dictionary<(int Pid, IntPtr Handle), IntPtr> _originalMasks = [];

    private bool _isOurCpuAbleToSupportAffinity = true;
    private Lock _lock = new ();
    private bool _isRunning = false;
    private bool _stopped;

    public CpuAffinityService(ILogger logger, IThumbnailConfiguration config)
    {
        _logger = logger;
        _config = config;
        DetectCores();
        _logger.WithCallerInfo().Verbose($"Auto detected CPU Architecture with {PCores.Count} Performance Cores and {ECores.Count} Efficiency Cores.");

        PreCalculateZones();
    }

    public void UpdateAffinity(IProcessInfo active, IProcessInfo next, IProcessInfo prev, IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            if (_stopped || !_isOurCpuAbleToSupportAffinity || !_config.EnableAutomaticCpuAffinity) return;
            var clients = allClients.Where(p => p != null).ToList();
            var liveKeys = clients.Select(p => (p.ProcessId, p.ProcessHandle)).ToHashSet();
            _currentBackgroundHandles.IntersectWith(liveKeys);
            foreach (var key in _originalMasks.Keys.Where(k => !liveKeys.Contains(k)).ToArray()) _originalMasks.Remove(key);
            active ??= next ?? prev;
            if (next?.ProcessId == active?.ProcessId) next = null;
            if (prev?.ProcessId == active?.ProcessId || prev?.ProcessId == next?.ProcessId) prev = null;

            // A process can have several HWNDs/records. The highest priority role wins once.
            foreach (var client in clients.GroupBy(p => p.ProcessId).Select(g => g.First()))
            {
                var key = (client.ProcessId, client.ProcessHandle);
                IntPtr mask = client.ProcessId == active?.ProcessId ? _activeMask
                    : client.ProcessId == next?.ProcessId ? _nextMask
                    : client.ProcessId == prev?.ProcessId ? _prevMask : _backgroundMask;
                bool background = client.ProcessId != active?.ProcessId && client.ProcessId != next?.ProcessId && client.ProcessId != prev?.ProcessId;
                if (background && _currentBackgroundHandles.Contains(key)) continue;
                _currentBackgroundHandles.Remove(key);
                if (WithHandle(client, handle =>
                {
                    if (!_originalMasks.TryGetValue(key, out IntPtr original))
                    {
                        if (!GetProcessAffinityMask(handle, out original, out _)) return false;
                        _originalMasks[key] = original;
                    }
                    // Respect restrictions the user or launcher applied before automation.
                    IntPtr allowed = (IntPtr)(mask.ToInt64() & original.ToInt64());
                    if (allowed == IntPtr.Zero) allowed = original;
                    return SetProcessAffinityMask(handle, allowed);
                }))
                {
                    _isRunning = true;
                    if (background) _currentBackgroundHandles.Add(key);
                }
            }
        }
    }

    public void ResetAll(IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            foreach (var client in allClients.Where(p => p != null))
            {
                var key = (client.ProcessId, client.ProcessHandle);
                if (_originalMasks.TryGetValue(key, out IntPtr original) &&
                    WithHandle(client, handle => SetProcessAffinityMask(handle, original)))
                    _originalMasks.Remove(key);
            }
            _currentBackgroundHandles.Clear();
            _isRunning = _originalMasks.Count > 0;
        }
    }

    public void Stop(IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            _stopped = true;
            ResetAll(allClients);
        }
    }

    private bool WithHandle(IProcessInfo process, Func<IntPtr, bool> operation)
    {
        var owned = (process as ProcessInfo)?.OwnedHandle;
        bool addedRef = false;
        try
        {
            owned?.DangerousAddRef(ref addedRef);
            IntPtr handle = process.ProcessHandle;
            if (handle == IntPtr.Zero || GetProcessId(handle) != process.ProcessId) return false;
            return operation(handle);
        }
        catch (ObjectDisposedException) { return false; }
        finally { if (addedRef) owned.DangerousRelease(); }
    }

    private void DetectCores()
    {
        uint length = 0;
        GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, ref length);

        IntPtr buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, buffer, ref length))
            {
                IntPtr current = buffer;
                int offset = 0;

                while (offset < length)
                {
                    var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(current);

                    if (info.Size < Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>() || offset + info.Size > length) break;
                    var core = info.Processor;
                    if (core.GroupCount != 1 || core.GroupMask.Group != 0)
                    {
                        _isOurCpuAbleToSupportAffinity = false;
                        _logger.Information("CPU affinity automation is unavailable across multiple processor groups");
                        return;
                    }

                    if (_logger.IsEnabled(LogEventLevel.Verbose))
                    {
                        var data = JsonConvert.SerializeObject(info);
                        _logger.Verbose($"Core Information: {data}");
                    }

                    // EfficiencyClass: Higher is better (P-Core), Lower is 0 (E-Core)
                    // GroupMask contains the logical processor bits
                    byte effClass = core.EfficiencyClass;
                    ulong mask = core.GroupMask.Mask;

                    for (int i = 0; i < MaxBitsPerGroup; i++)
                    {
                        if ((mask & (SingleCoreBit << i)) != 0)
                        {
                            if (effClass > 0)
                            {
                                PCores.Add(i);
                            }
                            else
                            {
                                ECores.Add(i);
                            }
                        }
                    }

                    offset += info.Size;
                    current = IntPtr.Add(current, info.Size);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        // Homogeneous CPUs report efficiency class zero for every core.
        if (PCores.Count == 0 && ECores.Count > 0)
        {
            PCores.AddRange(ECores);
            ECores.Clear();
        }
        // Fallback only within the one processor group this strategy supports.
        if (ECores.Count == 0 && PCores.Count == 0)
        {
            if (Environment.ProcessorCount > 64) { _isOurCpuAbleToSupportAffinity = false; return; }
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                PCores.Add(i);
            }
        }
    }

    private void PreCalculateZones()
    {
        var pThreads = PCores;
        int pCount = pThreads.Count;

        _allCoresMask = CreateMask(PCores.Concat(ECores));

        // One-time logic to decide how to best divide the CPU into zones.
        //
        // The clients that matter for responsiveness (active, predicted next, previous) are never
        // confined: they may use every performance thread. Earlier versions gave the active client
        // only two logical processors, i.e. the two SMT siblings of a single physical core, which
        // capped the frame rate of the client being played. Only background clients are moved away
        // from the performance cores, which is where the benefit of affinity management lies.
        if (pCount >= 4)
        {
            _logger.WithCallerInfo().Information("Foreground clients may use all {Count} performance threads; background clients are confined.", pCount);
            _activeMask = CreateMask(pThreads);
            _nextMask = _activeMask;
            _prevMask = _activeMask;
            // Without E-Cores, keep background clients on the upper half of the performance threads so
            // they cannot occupy every core the foreground client wants. Overridden below when E-Cores exist.
            _backgroundMask = CreateMask(pThreads.Skip(pCount / 2));
        }
        else // Low: Not enough threads available to be worth manually managing affinity.
        {
            _isOurCpuAbleToSupportAffinity = false;
            return;
        }

        if (ECores.Count > 0) // If we have E-Cores then use them for the background clients. Otherwise, just use the remaining cores.
        {
            _logger.WithCallerInfo().Information("Using E-Cores for all other background clients.");
            _backgroundMask = CreateMask(ECores);
        }

        _logger.WithCallerInfo().Verbose($"_activeMask = {_activeMask}, _nextMask = {_nextMask}, _prevMask = {_prevMask}, _backgroundMask = {_backgroundMask}");
    }
    
    private IntPtr CreateMask(IEnumerable<int> indices)
    {
        ulong mask = 0;
        foreach (int i in indices)
        {
            mask |= (SingleCoreBit << i);
        }

        return (IntPtr)mask;
    }
}