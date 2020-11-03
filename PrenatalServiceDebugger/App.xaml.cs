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
            File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", string.Join(", ", args.Args) + Environment.NewLine);
            try
            {
                if (args.Args.Length > 1 && args.Args[0] == "--Debug")
                {
                    File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "Debugging" + Environment.NewLine);
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

                    File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "As System service" + Environment.NewLine);

                    string debuggeeExecutable = args.Args[1];
                    var debuggeeArguments = args.Args.Skip(2);
                    string waitingUiExecutable = Assembly.GetExecutingAssembly().Location;
                    var waitingUiArguments = new List<string> { "--Wait", $"\"{Path.GetFileName(debuggeeExecutable)}\"" };

                    File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "debuggee proc: " + debuggeeExecutable + " " + string.Join(" ", debuggeeArguments) + Environment.NewLine);
                    File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "waiting proc: " + waitingUiExecutable + " " + string.Join(" ", waitingUiArguments) + Environment.NewLine);

                    using (var debuggeeProcess = new Process(debuggeeExecutable, debuggeeArguments))
                    using (var waitingProcess = new Process(waitingUiExecutable, waitingUiArguments))
                    {
                        try
                        {
                            // Bypass ImageFileExecutionOptions.Debugger, so the actual debuggee executable is started.
                            using (new ImageFileExecutionOptionsDebuggerBypass(Path.GetFileName(debuggeeExecutable)))
                            {
                                debuggeeProcess.Start(true);
                            }
                        }
                        catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                        {
                            // Unable to start the debugee
                            Current.Shutdown();
                            return;
                        }

                        try
                        {
                            // Start the waiting UI on the user desktop or on Winlogon screen when no user is logged on.
                            if (SystemUtils.IsLoggedOnUserAvailable())
                            {
                                File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "User available" + Environment.NewLine);
                                waitingProcess.StartOnUserDesktop();
                            }
                            else
                            {
                                // Wait for logon screen to become active
                                for (int i = 0; i < 10; ++i)
                                {
                                    uint activeSessionId = Process.GetActiveConsoleSessionId();

                                    if (activeSessionId != NativeMethods.InvalidSessionId)
                                    {
                                        break;
                                    }

                                    System.Threading.Thread.Sleep(1000);
                                }

                                File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "Logon screen available" + Environment.NewLine);
                                waitingProcess.StartOnLogonScreen();
                            }
                        }
                        catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                        {
                            // Too bad, but nothing we can do about it, just omit the waiting dialog.
                            File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "Unable to start wait window." + Environment.NewLine + e.StackTrace + Environment.NewLine + e.Message + Environment.NewLine);
                        }

                        // TODO: Also wait for waiting process exit (user closed the waiting window on purpose -> just resume service)
                        bool isDebuggerAttached = debuggeeProcess.WaitForDebugger(SystemUtils.GetServiceTimeout() - 2000);

                        if (isDebuggerAttached)
                        {
                            File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "Resume service." + Environment.NewLine);
                            debuggeeProcess.Resume();
                        }
                        else
                        {
                            File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "Terminate service." + Environment.NewLine);
                            debuggeeProcess.Terminate();
                        }

                        waitingProcess.Terminate();
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
                        File.AppendAllText(@"C:\Workbench\operatingtable\pnsd.log", "No args as local system?! Quit!." + Environment.NewLine);
                        Current.Shutdown();
                        return;
                    }

                    if (!SystemUtils.IsAdministrator())
                    {
                        // The configuration UI needs to run with elevated privileges.
                        // therefore, restart the program and run as administrator.
                        // Note: This is not done with manifest, to be able to start same exe as user from service for the waiting for debugger UI.
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

                    // TODO: Show settings window
                    //       Things to do in the settings window:
                    //       1. Check service timeout and prompt user to change it and restart system
                    // TODO: Test restart when timeout is changed
                    // TODO: remove logs
                    this.window = new MainWindow();
                    this.window.Show();
                }
            }
            catch (Exception e)
            {
                File.AppendAllText(@"C:\Workbench\operatingtable\pnsd_err.log", e.ToString() + Environment.NewLine);
            }
        }
    }
}
