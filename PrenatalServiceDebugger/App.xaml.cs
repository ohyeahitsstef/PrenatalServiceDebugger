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
        /// <summary>
        /// The start-up handler used by the application (set in XAML).
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The application start-up event arguments.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "False-positive: ImageFileExecutionOptionsDebuggerBypass may not be disposed multiple times.")]
        private void StartupHandler(object sender, StartupEventArgs args)
        {
            File.AppendAllText(@".\pnsd.log", string.Join(", ", args.Args) + Environment.NewLine);
            try
            {
                if (args.Args.Length > 1 && args.Args[0] == "--Debug")
                {
                    File.AppendAllText(@".\pnsd.log", "Debugging" + Environment.NewLine);
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

                    File.AppendAllText(@".\pnsd.log", "As System service" + Environment.NewLine);

                    string debuggeeExecutable = args.Args[1];
                    var debuggeeArguments = args.Args.Skip(2);
                    string waitingUiExecutable = Assembly.GetExecutingAssembly().Location;
                    var waitingUiArguments = new List<string> { "--Wait", $"\"{Path.GetFileName(debuggeeExecutable)}\"" };

                    File.AppendAllText(@".\pnsd.log", "debuggee proc: " + debuggeeExecutable + " " + string.Join(" ", debuggeeArguments) + Environment.NewLine);
                    File.AppendAllText(@".\pnsd.log", "waiting proc: " + waitingUiExecutable + " " + string.Join(" ", waitingUiArguments) + Environment.NewLine);

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
                            // TODO: handling if debuggee could not be started.
                        }

                        try
                        {
                            // TODO: Start on Winlogon screen when no user is logged on.
                            waitingProcess.StartOnUserDesktop();
                        }
                        catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                        {
                            // Too bad, but nothing we can do about it, just omit the waiting dialog.
                        }

                        bool isDebuggerAttached = debuggeeProcess.WaitForDebugger(SystemUtils.GetServiceTimeout() - 1000);

                        if (isDebuggerAttached)
                        {
                            debuggeeProcess.Resume();
                        }
                        else
                        {
                            debuggeeProcess.Terminate();
                        }

                        waitingProcess.Terminate();
                        Current.Shutdown();
                        return;
                    }
                }
                else if (args.Args.Length > 1 && args.Args[0] == "--Wait")
                {
                    using (var window = new WaitWindow
                    {
                        ApplicationName = args.Args.Length > 1 ? args.Args[1] : string.Empty,
                        TimeWaitedInPercent = 50,
                    })
                    {
                        window.Show();
                    }
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
                    var window = new MainWindow();
                    window.Show();
                }
            }
            catch (Exception e)
            {
                File.AppendAllText(@".\pnsd2.log", e.ToString() + Environment.NewLine);
            }
        }
    }
}
