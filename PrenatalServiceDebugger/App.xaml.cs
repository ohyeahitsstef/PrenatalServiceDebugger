// <copyright file="App.xaml.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows;

    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App : Application
    {
        private Window window;

        /// <summary>
        /// The start-up handler used by the application (set in XAML).
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The application start-up event arguments.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "False-positive: ImageFileExecutionOptionsDebuggerBypass may not be disposed multiple times.")]
        private void StartupHandler(object sender, StartupEventArgs args)
        {
            if (args.Args.Length > 1 && args.Args[0] == "--Debug")
            {
                if (!SystemUtils.IsAdministrator() && !SystemUtils.IsLocalSystem())
                {
                    MessageBox.Show(
                        (string)Application.Current.FindResource("RequiresAdministrativePrivilegesMessage"),
                        (string)Application.Current.FindResource("RequiresAdministrativePrivilegesMessageTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Current.Shutdown();
                    return;
                }

                string debuggeeExecutable = args.Args[1];
                var debuggeeArguments = args.Args.Skip(2);
                string waitingUiExecutable = Assembly.GetExecutingAssembly().Location;
                var waitingUiArguments = new List<string> { "--Wait", $"\"{Path.GetFileName(debuggeeExecutable)}\"" };

                using (var debuggeeProcess = new Process(debuggeeExecutable, debuggeeArguments))
                using (var waitingProcess = new Process(waitingUiExecutable, waitingUiArguments))
                {
                    try
                    {
                        System.Threading.Thread.Sleep(15000);

                        // Obtain process id of the servcie control manager (services.exe)
                        int servicesPid = 0;
                        System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName("services");
                        foreach (var process in processes)
                        {
                            if (process.SessionId == 0)
                            {
                                servicesPid = process.Id;

                                // Set the debug privilege to be able to interact with the protected service control manager process.
                                SystemUtils.SetPrivilege(NativeMethods.SE_DEBUG_PRIVILEGE_NAME, true);
                                break;
                            }
                        }

                        // Bypass ImageFileExecutionOptions.Debugger, so the actual debuggee executable is started.
                        using (new ImageFileExecutionOptionsDebuggerBypass(Path.GetFileName(debuggeeExecutable)))
                        {
                            debuggeeProcess.Start(true, servicesPid);
                        }
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                    {
                        // Unable to start the debugee.
                        Current.Shutdown();
                        return;
                    }

                    try
                    {
                        // Start the waiting UI on the user desktop or on Winlogon screen when no user is logged on.
                        if (SystemUtils.IsLoggedOnUserAvailable())
                        {
                            waitingProcess.StartOnUserDesktop();
                        }
                        else
                        {
                            // Wait for logon screen to become active (max. wait time 120 seconds)
                            for (int i = 0; i < 120; ++i)
                            {
                                uint activeSessionId = Process.GetActiveConsoleSessionId();

                                if (activeSessionId != NativeMethods.InvalidSessionId)
                                {
                                    break;
                                }

                                System.Threading.Thread.Sleep(1000);
                            }

                            waitingProcess.StartOnLogonScreen();
                        }
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                    {
                        // Too bad, but nothing we can do about it, just omit the waiting dialog.
                    }

                    // Wait for waiting process to exit (user closed the waiting window on purpose -> just resume service)
                    // or for a debugger to be attached to the debuggee process.
                    var tasks = new List<Task<bool>>();
                    tasks.Add(debuggeeProcess.IsDebuggerPresentAsync());
                    if (waitingProcess.Started)
                    {
                        tasks.Add(waitingProcess.HasExitedAsync());
                    }

                    int index = Task.WaitAny(tasks.ToArray(), SystemUtils.GetServiceTimeout() - 2000);

                    debuggeeProcess.Resume();

                    // In case a debugger has been attached close the waiting window.
                    if (waitingProcess.Started && index == 0)
                    {
                        waitingProcess.Terminate();
                    }

                    // Wait for the debugee process to exit. This is required since exiting the current process would also exit the debugee.
                    // #7 We actually do no longer need this process as the debugee has been resumed.
                    // debuggeeProcess.HasExitedAsync().Wait();

                    Current.Shutdown();
                    return;
                }
            }
            else if (args.Args.Length > 1 && args.Args[0] == "--Wait")
            {
                this.window = new WaitWindow
                {
                    ApplicationName = args.Args.Length > 1 ? args.Args[1] : string.Empty,
                    TimeWaitedInPercent = 50,
                };
                this.window.Show();
                this.window.Activate();
            }
            else
            {
                if (SystemUtils.IsLocalSystem())
                {
                    // If we accidentally got started as a service with invalid command line arguments, just quit.
                    Current.Shutdown();
                    return;
                }

                if (!SystemUtils.IsAdministrator())
                {
                    // The configuration UI needs to run with elevated privileges.
                    // therefore, restart the program and run as administrator.
                    // Note: This is not done with manifest, to be able to start same executable as user from service (for the waiting UI).
                    using (var process = new Process(Assembly.GetExecutingAssembly().Location, null))
                    {
                        try
                        {
                            process.StartAsAdmin();
                        }
                        catch (InvalidOperationException e)
                        {
                            MessageBox.Show($"An unexpected error occurred during program restart.{Environment.NewLine}{e.Message}", "Error restarting program", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        Current.Shutdown();
                        return;
                    }
                }

                this.window = new MainWindow();
                this.window.Show();
            }
        }
    }
}
