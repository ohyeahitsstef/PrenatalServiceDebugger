// <copyright file="SystemUtils.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.ComponentModel;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Principal;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using Microsoft.Win32;

    /// <summary>
    /// Class with system utility functions.
    /// </summary>
    internal static class SystemUtils
    {
        /// <summary>
        /// The default service timeout.
        /// </summary>
        public const int ServiceTimeoutDefault = 30000;

        /// <summary>
        /// The registry root key for services.
        /// </summary>
        private const string CurrentControlSetRegistryKey = @"SYSTEM\CurrentControlSet\Control";

        /// <summary>
        /// The name of the service timeout registry value.
        /// </summary>
        private const string ServicesPipeTimeoutRegistryValue = "ServicesPipeTimeout";

        /// <summary>
        /// The registry root key for image file execution options.
        /// </summary>
        private const string ImageFileExecutionOptionsRegistryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

        /// <summary>
        /// The name of the debugger registry value.
        /// </summary>
        private const string DebuggerRegistryValue = "Debugger";

        /// <summary>
        /// System icons enumeration.
        /// </summary>
        internal enum SystemIcon : int
        {
            /// <summary>
            /// Info icon.
            /// </summary>
            Info = (int)NativeMethods.SHSTOCKICONID.SIID_INFO,

            /// <summary>
            /// Warning icon.
            /// </summary>
            Warning = (int)NativeMethods.SHSTOCKICONID.SIID_WARNING,

            /// <summary>
            /// Error icon
            /// </summary>
            Error = (int)NativeMethods.SHSTOCKICONID.SIID_ERROR,
        }

        /// <summary>
        /// System icon size enumeration.
        /// </summary>
        internal enum SystemIconSize : int
        {
            /// <summary>
            /// Small icon size.
            /// </summary>
            Small = (int)NativeMethods.SHGSI.SHGSI_SMALLICON,

            /// <summary>
            /// Large icon size.
            /// </summary>
            Large = (int)NativeMethods.SHGSI.SHGSI_LARGEICON,
        }

        /// <summary>
        /// Gets a native system icon as a managed <see cref="BitmapSource"/>.
        /// </summary>
        /// <param name="icon">The system icon.</param>
        /// <param name="iconSize">The system icon size.</param>
        /// <returns>Returns the system icon as <see cref="BitmapSource"/>.</returns>
        public static BitmapSource GetSystemIcon(SystemIcon icon, SystemIconSize iconSize)
        {
            NativeMethods.SHSTOCKICONINFO sii = default(NativeMethods.SHSTOCKICONINFO);
            sii.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.SHSTOCKICONINFO));

            Marshal.ThrowExceptionForHR(NativeMethods.SHGetStockIconInfo((NativeMethods.SHSTOCKICONID)icon, (NativeMethods.SHGSI)iconSize | NativeMethods.SHGSI.SHGSI_ICON, ref sii));

            var iconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(sii.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            NativeMethods.DestroyIcon(sii.hIcon);
            return iconSource;
        }

        /// <summary>
        /// Checks whether the current process runs as administrator or not.
        /// </summary>
        /// <returns>Returns true if the current process runs as administrator, otherwise false is returned.</returns>
        public static bool IsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception e) when (e is SecurityException || e is ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks whether the current process runs as system or not.
        /// </summary>
        /// <returns>Returns true if the current process runs as system, otherwise false is returned.</returns>
        public static bool IsLocalSystem()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                return principal.IsInRole(localSystemSid);
            }
            catch (Exception e) when (e is SecurityException || e is ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current service timeout.
        /// </summary>
        /// <returns>Returns the service timeout in milliseconds.</returns>
        public static int GetServiceTimeout()
        {
            using (var regkey = Registry.LocalMachine.OpenSubKey(CurrentControlSetRegistryKey, false))
            {
                return regkey.GetValue(ServicesPipeTimeoutRegistryValue) as int? ?? ServiceTimeoutDefault;
            }
        }

        /// <summary>
        /// Sets the service timeout.
        /// This requires a reboot to take effect.
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds.</param>
        public static void SetServiceTimeout(int timeout)
        {
            // Requires reboot to take effect.
            using (var regkey = Registry.LocalMachine.OpenSubKey(CurrentControlSetRegistryKey, true))
            {
                regkey.SetValue(ServicesPipeTimeoutRegistryValue, timeout);
            }
        }

        /// <summary>
        /// Sets the default service timeout.
        /// This requires a reboot to take effect.
        /// </summary>
        public static void SetDefaultServiceTimeout()
        {
            // Requires reboot to take effect.
            using (var regkey = Registry.LocalMachine.OpenSubKey(CurrentControlSetRegistryKey, true))
            {
                regkey.DeleteValue(ServicesPipeTimeoutRegistryValue);
            }
        }

        /// <summary>
        /// Restarts the system with a custom message and a timeout.
        /// </summary>
        /// <param name="message">The restart message shown to the user.</param>
        /// <param name="timeout">The time to be waited before the restart happens.</param>
        public static void Restart(string message, TimeSpan timeout)
        {
            var luid = default(NativeMethods.LUID);
            var privilegeFound = NativeMethods.LookupPrivilegeValueW(null, NativeMethods.SE_SHUTDOWN_PRIVILEGE_NAME, ref luid);
            if (!privilegeFound)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Could not find value for privilege {NativeMethods.SE_SHUTDOWN_PRIVILEGE_NAME}.");
            }

            var privilegesToken = new NativeMethods.PrivilegesToken
            {
                Privileges = new int[]
                {
                    luid.LowPart,
                    luid.HighPart,
                    NativeMethods.SE_PRIVILEGE_ENABLED,
                },
                PrivilegeCount = 1,
            };

            var tokenHandle = IntPtr.Zero;
            var process = System.Diagnostics.Process.GetCurrentProcess();

            if (process.Handle == IntPtr.Zero)
            {
                throw new Win32Exception(0, $"Could not get handle to current process.");
            }

            var processTokenOpened = NativeMethods.OpenProcessToken(
                process.Handle,
                NativeMethods.TOKEN_QUERY | NativeMethods.TOKEN_ADJUST_PRIVILEGES,
                out tokenHandle);

            if (!processTokenOpened)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Could not open process token.");
            }

            var bufferPtr = Marshal.AllocHGlobal(Marshal.SizeOf(privilegesToken));
            Marshal.StructureToPtr(privilegesToken, bufferPtr, true);
            int returnLength = 0;

            var tokenPrivilegesAdjusted = NativeMethods.AdjustTokenPrivileges(
                tokenHandle,
                0,
                bufferPtr,
                0,
                IntPtr.Zero,
                ref returnLength);

            int errorOfAdjustTokenPrivileges = Marshal.GetLastWin32Error();

            Marshal.DestroyStructure(bufferPtr, typeof(NativeMethods.PrivilegesToken));
            Marshal.FreeHGlobal(bufferPtr);
            NativeMethods.CloseHandle(tokenHandle);

            if (!tokenPrivilegesAdjusted || errorOfAdjustTokenPrivileges != 0)
            {
                throw new Win32Exception(errorOfAdjustTokenPrivileges, $"Could not adjust token privileges.");
            }

            var restartInitiated = NativeMethods.InitiateSystemShutdownExW(
                null,
                message,
                Convert.ToUInt32(timeout.TotalSeconds),
                false,
                true,
                NativeMethods.ShutdownReason.SHTDN_REASON_MAJOR_OPERATINGSYSTEM | NativeMethods.ShutdownReason.SHTDN_REASON_MINOR_RECONFIG | NativeMethods.ShutdownReason.SHTDN_REASON_FLAG_PLANNED);

            if (!restartInitiated)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Could not initiate restart.");
            }
        }

        /// <summary>
        /// Gets the configured IFEO debugger for an executable.
        /// </summary>
        /// <param name="executable">The file name of the executable.</param>
        /// <returns>Returns the configured debugger or null.</returns>
        public static string GetIfeoDebugger(string executable)
        {
            var registryPath = $@"{ImageFileExecutionOptionsRegistryKey}\{executable}";

            using (var regKey = Registry.LocalMachine.OpenSubKey(registryPath, false))
            {
                return regKey?.GetValue(DebuggerRegistryValue) as string;
            }
        }

        /// <summary>
        /// Sets the IFEO debugger for an executable.
        /// </summary>
        /// <param name="executable">The file name of the executable.</param>
        /// <param name="debugger">The debugger command line (Full path and command line arguments).</param>
        public static void SetIfeoDebugger(string executable, string debugger)
        {
            var registryPath = $@"{ImageFileExecutionOptionsRegistryKey}\{executable}";

            using (var regKey = Registry.LocalMachine.CreateSubKey(registryPath))
            {
                regKey?.SetValue(DebuggerRegistryValue, debugger);
            }
        }

        /// <summary>
        /// Removes the IFEO debugger for an executable.
        /// </summary>
        /// <param name="executable">The file name of the executable.</param>
        public static void RemoveIfeoDebugger(string executable)
        {
            var registryPath = $@"{ImageFileExecutionOptionsRegistryKey}\{executable}";

            using (var regKey = Registry.LocalMachine.OpenSubKey(registryPath, true))
            {
                regKey?.DeleteValue(DebuggerRegistryValue);
            }
        }

        /// <summary>
        /// Checks whether a specific IFEO debugger is configured for an executable or not.
        /// </summary>
        /// <param name="executable">The file name of the executable.</param>
        /// <param name="debugger">The file name of the debugger.</param>
        /// <returns>Returns true if the debugger is configured, otherwise false is returned.</returns>
        public static bool IsIfeoDebuggerSet(string executable, string debugger)
        {
            var currentDebugger = GetIfeoDebugger(executable);
            if (!string.IsNullOrEmpty(currentDebugger) && currentDebugger.Contains(debugger, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets whether a user is currently logged on to the system.
        /// </summary>
        /// <returns>Returns true if there is a logged on user.</returns>
        public static bool IsLoggedOnUserAvailable()
        {
            uint activeSessionId = Process.GetActiveConsoleSessionId();

            // If the session id is valid and not the system session.
            return activeSessionId != NativeMethods.InvalidSessionId && activeSessionId != 0;
        }
    }
}
