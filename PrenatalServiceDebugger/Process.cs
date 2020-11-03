// <copyright file="Process.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Class representing a process.
    /// </summary>
    internal sealed class Process : IDisposable
    {
        private SafeClosableHandle processHandle;
        private SafeClosableHandle threadHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="Process"/> class.
        /// </summary>
        /// <param name="executablePath">Path to the executable of the process.</param>
        /// <param name="args">Command line arguments for the process.</param>
        public Process(string executablePath, IEnumerable<string> args)
        {
            this.ExecutablePath = executablePath;
            this.Arguments = new List<string>(args ?? Enumerable.Empty<string>());
        }

        /// <summary>
        /// Gets the executable path of the process.
        /// </summary>
        public string ExecutablePath { get; }

        /// <summary>
        /// Gets the command line arguments of the process.
        /// </summary>
        public IList<string> Arguments { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.processHandle?.Dispose();
            this.threadHandle?.Dispose();
        }

        /// <summary>
        /// Starts the process.
        /// </summary>
        public void Start()
        {
            this.Start(false);
        }

        /// <summary>
        /// Starts the process either suspended or not.
        /// </summary>
        /// <param name="suspended">Indicates whether the process is started suspended or not.</param>
        public void Start(bool suspended)
        {
            NativeMethods.CreateProcessFlags creationFlags = NativeMethods.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT;
            if (suspended)
            {
                creationFlags |= NativeMethods.CreateProcessFlags.CREATE_SUSPENDED;
            }

            var startupInfo = default(NativeMethods.STARTUPINFO);
            var processInfo = default(NativeMethods.PROCESS_INFORMATION);

            bool processCreated = NativeMethods.CreateProcessW(
                null,
                this.GetCommandLine(),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                creationFlags,
                IntPtr.Zero,
                null,
                ref startupInfo,
                out processInfo);

            if (!processCreated)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create process.");
            }

            this.processHandle = new SafeClosableHandle(processInfo.hProcess);
            this.threadHandle = new SafeClosableHandle(processInfo.hThread);
        }

        /// <summary>
        /// Starts the process with administrator privileges.
        /// </summary>
        public void StartAsAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(
                Environment.ExpandEnvironmentVariables(this.ExecutablePath),
                string.Join(" ", this.Arguments))
            {
                Verb = "runas",
            };

            try
            {
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception e) when (
                e is InvalidOperationException ||
                e is ArgumentNullException ||
                e is ObjectDisposedException ||
                e is FileNotFoundException ||
                e is Win32Exception)
            {
                throw new InvalidOperationException("Failed to start program as administrator.", e);
            }
        }

        /// <summary>
        /// Starts the process as a user and in the user session and its environment.
        /// </summary>
        public void StartOnUserDesktop()
        {
            if (!SystemUtils.IsLocalSystem())
            {
                throw new InvalidOperationException("TODO");
            }

            uint sessionId = GetActiveConsoleSessionId();

            if (sessionId == NativeMethods.InvalidSessionId)
            {
                throw new Win32Exception($"No active console session found.");
            }

            // Get the user token. This requires certain privileges only held by Local System.
            IntPtr token;
            bool tokenReceived = NativeMethods.WTSQueryUserToken(sessionId, out token);
            if (!tokenReceived)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not acquire token for session {sessionId}.");
            }

            // Get user name from the session.
            IntPtr userNameBuffer;
            uint userNameBufferSize;
            bool nameReceived = NativeMethods.WTSQuerySessionInformation(NativeMethods.WTS_CURRENT_SERVER_HANDLE, sessionId, NativeMethods.WTS_INFO_CLASS.WTSUserName, out userNameBuffer, out userNameBufferSize);
            if (!nameReceived)
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.CloseHandle(token);
                throw new Win32Exception(error, $"Could not query user name for session {sessionId}.");
            }

            // Get the user profile with the token.
            var profile = default(NativeMethods.PROFILEINFO);
            profile.dwSize = Marshal.SizeOf(profile);
            profile.lpUserName = Marshal.PtrToStringAnsi(userNameBuffer);

            bool isProfileLoaded = NativeMethods.LoadUserProfileW(token, ref profile);
            if (!isProfileLoaded)
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.WTSFreeMemory(userNameBuffer);
                NativeMethods.CloseHandle(token);
                throw new Win32Exception(error, $"Could not load user profile for session {sessionId}.");
            }

            // Get the user environment with the token.
            IntPtr environment;
            bool gotEnvironment = NativeMethods.CreateEnvironmentBlock(out environment, token, false);
            if (!gotEnvironment)
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.UnloadUserProfile(token, profile.hProfile);
                NativeMethods.WTSFreeMemory(userNameBuffer);
                NativeMethods.CloseHandle(token);

                throw new Win32Exception(error, $"Could not create environment block for user {profile.lpUserName} in session {sessionId}.");
            }

            // Get the profile directory, optional.
            string profileDir = null;
            StringBuilder profileDirBuilder = new StringBuilder(500);
            uint profileDirSize = (uint)profileDirBuilder.Capacity;
            bool gotUserProfile = NativeMethods.GetUserProfileDirectoryW(token, profileDirBuilder, ref profileDirSize);
            if (gotUserProfile)
            {
                profileDir = profileDirBuilder.ToString();
            }

            var startupInfo = default(NativeMethods.STARTUPINFO);
            startupInfo.cb = Marshal.SizeOf(startupInfo);
            NativeMethods.PROCESS_INFORMATION processInfo;

            var processSecurityAttributes = default(NativeMethods.SECURITY_ATTRIBUTES);
            var threadSecurityAttributes = default(NativeMethods.SECURITY_ATTRIBUTES);

            bool processCreated = NativeMethods.CreateProcessAsUserW(
                token,
                null,
                this.GetCommandLine(),
                ref processSecurityAttributes,
                ref threadSecurityAttributes,
                false,
                NativeMethods.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT,
                environment,
                profileDir,
                ref startupInfo,
                out processInfo);
            if (!processCreated)
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.DestroyEnvironmentBlock(environment);
                NativeMethods.UnloadUserProfile(token, profile.hProfile);
                NativeMethods.WTSFreeMemory(userNameBuffer);
                NativeMethods.CloseHandle(token);
                throw new Win32Exception(error, $"Could not create process for session {sessionId} and user {profile.lpUserName}.");
            }

            NativeMethods.DestroyEnvironmentBlock(environment);
            NativeMethods.UnloadUserProfile(token, profile.hProfile);
            NativeMethods.WTSFreeMemory(userNameBuffer);
            NativeMethods.CloseHandle(token);

            this.processHandle = new SafeClosableHandle(processInfo.hProcess);
            this.threadHandle = new SafeClosableHandle(processInfo.hThread);
        }

        /// <summary>
        /// Starts the process in the Winlogon session.
        /// </summary>
        public void StartOnLogonScreen()
        {
            if (!SystemUtils.IsLocalSystem())
            {
                throw new InvalidOperationException("TODO");
            }

            uint sessionId = GetActiveConsoleSessionId();

            if (sessionId == NativeMethods.InvalidSessionId)
            {
                throw new Win32Exception($"No active console session found.");
            }

            // Obtain the process ID of the winlogon process that is running within the currently active session.
            uint winlogonPid = 0;
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName("winlogon");
            foreach (var p in processes)
            {
                if ((uint)p.SessionId == sessionId)
                {
                    winlogonPid = (uint)p.Id;
                }
            }

            if (winlogonPid == 0)
            {
                throw new Win32Exception($"Winlogon process not found.");
            }

            // Obtain a handle to the winlogon process.
            var winlogonHandle = NativeMethods.OpenProcess(NativeMethods.ACCESS_MASK.MAXIMUM_ALLOWED, false, winlogonPid);

            // Get the token of the winlogon process.
            IntPtr currentToken = IntPtr.Zero;
            bool processTokenOpened = NativeMethods.OpenProcessToken(
                winlogonHandle,
                NativeMethods.TOKEN_DUPLICATE,
                out currentToken);

            NativeMethods.CloseHandle(winlogonHandle);
            if (!processTokenOpened)
            {
                throw new Win32Exception();
            }

            // Duplicate token
            var securityAttributes = default(NativeMethods.SECURITY_ATTRIBUTES);
            securityAttributes.nLength = Marshal.SizeOf(securityAttributes);

            var newToken = IntPtr.Zero;
            bool tokenDuplicated = NativeMethods.DuplicateTokenEx(
                currentToken,
                NativeMethods.ACCESS_MASK.MAXIMUM_ALLOWED,
                ref securityAttributes,
                NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                NativeMethods.TOKEN_TYPE.TokenPrimary,
                out newToken);

            NativeMethods.CloseHandle(currentToken);
            if (!tokenDuplicated)
            {
                throw new Win32Exception();
            }

            // Finally create process
            var processInfo = default(NativeMethods.PROCESS_INFORMATION);

            var processSecurityAttributes = default(NativeMethods.SECURITY_ATTRIBUTES);
            processSecurityAttributes.nLength = Marshal.SizeOf(processSecurityAttributes);

            var threadSecurityAttributes = default(NativeMethods.SECURITY_ATTRIBUTES);
            threadSecurityAttributes.nLength = Marshal.SizeOf(threadSecurityAttributes);

            var startupInfo = default(NativeMethods.STARTUPINFO);
            startupInfo.cb = Marshal.SizeOf(startupInfo);
            startupInfo.lpDesktop = @"winsta0\Winlogon";

            bool processCreated = NativeMethods.CreateProcessAsUserW(
                newToken,
                null,
                this.GetCommandLine(),
                ref processSecurityAttributes,
                ref threadSecurityAttributes,
                false,
                NativeMethods.CreateProcessFlags.CREATE_NEW_CONSOLE | NativeMethods.CreateProcessFlags.NORMAL_PRIORITY_CLASS,
                IntPtr.Zero,
                null,
                ref startupInfo,
                out processInfo);

            NativeMethods.CloseHandle(newToken);

            if (!processCreated)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not create process for session {sessionId} on desktop {startupInfo.lpDesktop}.");
            }

            this.processHandle = new SafeClosableHandle(processInfo.hProcess);
            this.threadHandle = new SafeClosableHandle(processInfo.hThread);
        }

        /// <summary>
        /// Suspends the process (main thread).
        /// </summary>
        public void Suspend()
        {
            if (this.threadHandle == null)
            {
                return;
            }

            int result = NativeMethods.SuspendThread(this.threadHandle.DangerousGetHandle());
            if (result == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Resumes a suspended process (main thread).
        /// </summary>
        public void Resume()
        {
            if (this.threadHandle == null)
            {
                return;
            }

            int result = NativeMethods.ResumeThread(this.threadHandle.DangerousGetHandle());
            if (result == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Waits until a debugger is attached to the process or a timeout occurs.
        /// </summary>
        /// <param name="timeout">The number of milliseconds to be waited for a debugger.</param>
        /// <returns>Returns true if a debugger has attached, otherwise false is returned.</returns>
        public bool WaitForDebugger(int timeout)
        {
            if (this.processHandle == null)
            {
                return false;
            }

            var task = Task<bool>.Factory.StartNew(() =>
            {
                const int delayTime = 1000;
                var passedTime = 0;
                var failuresCount = 0;
                var isDebuggerPresent = false;
                while (failuresCount <= 3 && passedTime <= timeout)
                {
                    if (!NativeMethods.CheckRemoteDebuggerPresent(this.processHandle.DangerousGetHandle(), ref isDebuggerPresent))
                    {
                        failuresCount++;
                    }

                    if (isDebuggerPresent)
                    {
                        break;
                    }

                    // Retry after some delay
                    Task.Delay(delayTime).Wait();
                    passedTime += delayTime;
                }

                return isDebuggerPresent;
            });
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Terminates the process.
        /// </summary>
        public void Terminate()
        {
            if (this.processHandle == null)
            {
                return;
            }

            //TODO: Check process handle if it is still active GetExitCodeProcess() STILL_ACTIVE

            bool processTerminated = NativeMethods.TerminateProcess(this.processHandle.DangerousGetHandle(), 1);
            if (!processTerminated)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not terminate process");
            }
        }

        /// <summary>
        /// Gets the session id of the currently active console session (local and RDP).
        /// </summary>
        /// <returns>Returns the session id of the currently active console session.</returns>
        internal static uint GetActiveConsoleSessionId()
        {
            uint level = 1;
            IntPtr sessions = IntPtr.Zero;
            uint sessionCount = 0;

            // Enumerate all sessions
            bool hasEnumeratedSessions = NativeMethods.WTSEnumerateSessionsExW(NativeMethods.WTS_CURRENT_SERVER_HANDLE, ref level, 0, ref sessions, ref sessionCount);
            if (!hasEnumeratedSessions)
            {
                return NativeMethods.InvalidSessionId;
            }

            int dataSize = Marshal.SizeOf(typeof(NativeMethods.WTS_SESSION_INFO_1));
            IntPtr currentSessionPtr = sessions;
            uint consoleSession = NativeMethods.InvalidSessionId;

            // Iterate over all sessions and find the active console/rdp session.
            for (uint i = 0; i < sessionCount && consoleSession == NativeMethods.InvalidSessionId; ++i)
            {
                NativeMethods.WTS_SESSION_INFO_1 sessionInfo = (NativeMethods.WTS_SESSION_INFO_1)Marshal.PtrToStructure(currentSessionPtr, typeof(NativeMethods.WTS_SESSION_INFO_1));
                currentSessionPtr = IntPtr.Add(currentSessionPtr, dataSize);

                if (sessionInfo.State != NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive)
                {
                    continue;
                }

                IntPtr protocolType;
                uint protocolTypeLength;
                bool hasQueriedInfo = NativeMethods.WTSQuerySessionInformation(NativeMethods.WTS_CURRENT_SERVER_HANDLE, sessionInfo.SessionId, NativeMethods.WTS_INFO_CLASS.WTSClientProtocolType, out protocolType, out protocolTypeLength);
                if (!hasQueriedInfo)
                {
                    continue;
                }

                ushort type = (ushort)Marshal.ReadInt16(protocolType);
                NativeMethods.WTSFreeMemory(protocolType);
                if (type == (ushort)NativeMethods.WTSClientProtocolType.Console ||
                    type == (ushort)NativeMethods.WTSClientProtocolType.Rdp)
                {
                    // We found it!
                    consoleSession = sessionInfo.SessionId;
                }
            }

            NativeMethods.WTSFreeMemoryExW(NativeMethods.WTS_TYPE_CLASS.WTSTypeSessionInfoLevel1, sessions, sessionCount);

            return consoleSession;
        }

        /// <summary>
        /// Gets the complete command line of the process (Path and arguments).
        /// </summary>
        /// <returns>Returns the complete command line.</returns>
        private string GetCommandLine()
        {
            string commandLine = Environment.ExpandEnvironmentVariables(this.ExecutablePath);
            if (commandLine.Contains(" "))
            {
                commandLine = $"\"{commandLine}\"";
            }

            if (this.Arguments == null || this.Arguments.Count == 0)
            {
                return commandLine;
            }

            commandLine += $" {string.Join(" ", this.Arguments)}";
            return commandLine;
        }
    }
}
