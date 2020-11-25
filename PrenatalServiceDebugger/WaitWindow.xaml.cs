// <copyright file="WaitWindow.xaml.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for WaitWindow.xaml.
    /// </summary>
    public partial class WaitWindow : Window, INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// The tick in milliseconds used for waiting for the timeout.
        /// </summary>
        private const int WindowTimeoutTick = 1000;

        /// <summary>
        /// The timer used for waiting for the timeout.
        /// </summary>
        private Timer windowTimeoutTimer;

        /// <summary>
        /// The timeout used for waiting.
        /// </summary>
        private int windowTimeout = SystemUtils.GetServiceTimeout();

        /// <summary>
        /// The elapsed waiting time.
        /// </summary>
        private int elapsedTime;

        /// <summary>
        /// The percentage of the elapsed time in regards to the overall waiting time.
        /// </summary>
        private int timeWaitedInPercent;

        /// <summary>
        /// The name of the application shown in the UI.
        /// </summary>
        private string applicationName;

        /// <summary>
        /// Indicates whether or not the instance is already disposed.
        /// </summary>
        private bool alreadyDisposed;

        /// <summary>
        /// The command for canceling the waiting window.
        /// </summary>
        private ICommand cancelCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="WaitWindow"/> class.
        /// </summary>
        public WaitWindow()
        {
            this.InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.windowTimeoutTimer = new Timer(this.Tick, null, 0, WindowTimeoutTick);
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the image to be shown in the dialog.
        /// </summary>
        public static ImageSource DialogImage { get => SystemUtils.GetSystemIcon(SystemUtils.SystemIcon.Info, SystemUtils.SystemIconSize.Large); }

        /// <summary>
        /// Gets or sets the name of the application shown in the UI.
        /// </summary>
        public string ApplicationName
        {
            get
            {
                return this.applicationName;
            }

            set
            {
                this.applicationName = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ApplicationName)));
            }
        }

        /// <summary>
        /// Gets or sets the time waited in percent.
        /// </summary>
        public int TimeWaitedInPercent
        {
            get
            {
                return this.timeWaitedInPercent;
            }

            set
            {
                this.timeWaitedInPercent = value > 100 ? 100 : value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.TimeWaitedInPercent)));
            }
        }

        /// <summary>
        /// Gets the command for canceling the waiting window.
        /// </summary>
        public ICommand CancelCommand
        {
            get
            {
                return this.cancelCommand ?? (this.cancelCommand = new DelegateCommand((x) => this.ExecuteCancel(), (x) => true));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the resources that need to be freed.
        /// </summary>
        /// <param name="dispose">Indicates whether managed resources should also be disposed.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (this.alreadyDisposed)
            {
                return;
            }

            if (dispose)
            {
                this.windowTimeoutTimer?.Dispose();
            }

            this.alreadyDisposed = true;
        }

        /// <summary>
        /// The tick callback to update the progress on the waiting state of the window.
        /// </summary>
        /// <param name="stateInfo">The state info.</param>
        private void Tick(object stateInfo)
        {
            this.elapsedTime += WindowTimeoutTick;
            if (this.elapsedTime >= this.windowTimeout)
            {
                Action closeAction = () => this.Close();
                this.Dispatcher.Invoke(DispatcherPriority.Normal, closeAction);
            }

            this.TimeWaitedInPercent = (this.elapsedTime * 100) / this.windowTimeout;
        }

        /// <summary>
        /// Cancels the waiting window (by closing it).
        /// </summary>
        private void ExecuteCancel()
        {
            this.Close();
        }
    }
}
