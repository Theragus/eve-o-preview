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

using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;
using System;
using System.Diagnostics;

namespace EveOPreview.Helper
{
    public static class ProcessHelpers
    {
        public static IProcessInfo ToProcessInfo(this Process process)
        {
            if (process == null)
            {
                return null;
            }

            var hwnd = process.MainWindowHandle;
            var id = process.Id;
            var title = process.MainWindowTitle;
            return new ProcessInfo(hwnd, process.OpenKernelHandle(), id, title);
        }

        public static IntPtr OpenKernelHandle(this Process process)
        {
            // Open a lightweight Kernel MainWindowHandle for Affinity/Priority
            // 0x0200 = PROCESS_SET_INFORMATION
            // 0x1000 = PROCESS_QUERY_LIMITED_INFORMATION
            IntPtr pHandle = KernelNativeMethods.OpenProcess(0x1200, false, process.Id);

            return pHandle;
        }

        /// <summary>
        /// Opts the client out of Windows 11 power throttling. By default Windows classifies a process whose
        /// window is minimized or fully covered (for example an EVE client stacked behind the active one) as
        /// low Quality of Service: it is scheduled as EcoQoS and its timer resolution request is ignored.
        /// Both make the client's own frame pacing coarse and uneven, which is visible in its preview until it
        /// is focused again. Setting the control bits with a cleared state turns that throttling off for the
        /// process regardless of its window visibility. Requires PROCESS_SET_INFORMATION on the handle.
        /// </summary>
        public static bool TryDisablePowerThrottling(this IProcessInfo processInfo) =>
            processInfo.TrySetPowerThrottling(KernelNativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED | KernelNativeMethods.PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION);

        /// <summary>Returns the client to the system-managed power throttling behaviour.</summary>
        public static bool TryRestorePowerThrottling(this IProcessInfo processInfo) => processInfo.TrySetPowerThrottling(0);

        private static bool TrySetPowerThrottling(this IProcessInfo processInfo, uint controlMask)
        {
            IntPtr handle = processInfo?.ProcessHandle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                return false;
            }

            var state = new KernelNativeMethods.PROCESS_POWER_THROTTLING_STATE
            {
                Version = KernelNativeMethods.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = controlMask,
                StateMask = 0
            };

            try
            {
                return KernelNativeMethods.SetProcessInformation(handle, KernelNativeMethods.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                    ref state, (uint)System.Runtime.InteropServices.Marshal.SizeOf<KernelNativeMethods.PROCESS_POWER_THROTTLING_STATE>());
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
            {
                return false;
            }
        }

        public static void CloseKernelHandle(this IProcessInfo processInfo)
        {
            if (processInfo is ProcessInfo owned)
            {
                owned.Dispose();
                return;
            }
            if (processInfo == null || processInfo.ProcessHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                KernelNativeMethods.CloseHandle(processInfo.ProcessHandle);
            }
            catch
            {
                // Nothing to do here, just don't crash anything else.
            }
        }
    }
}