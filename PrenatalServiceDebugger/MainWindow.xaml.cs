// <copyright file="MainWindow.xaml.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Regular expression to extract an executable file name from a string.
        /// </summary>
        private static Regex executableFileNameRegEx = new Regex(@"[^\\]*.exe", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        /// <summary>
        /// List of services that are excluded from being used for debugging.
        /// </summary>
        private static List<string> excludedServices = new List<string> { "SVCHOST.EXE" };

        /// <summary>
        /// Custom service timeout in milliseconds.
        /// </summary>
        private int customServiceTimeout;

        /// <summary>
        /// Indicates whether a custom service timeout is used or not.
        /// </summary>
        private bool useCustomServiceTimeout;

        /// <summary>
        /// The command for setting the service timeout.
        /// </summary>
        private ICommand setServiceTimeoutCommand;

        /// <summary>
        /// The command for toggling the debugger.
        /// </summary>
        private ICommand toggleDebuggerCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            GetServices().ToList().ForEach(service =>
                {
                    service.PropertyChanged += this.ServiceModelPropertyChanged;
                    this.Services.Add(service);
                });
            this.customServiceTimeout = SystemUtils.GetServiceTimeout();
            this.useCustomServiceTimeout = this.customServiceTimeout != SystemUtils.ServiceTimeoutDefault;

            this.InitializeComponent();

            // Hide info field for service timeout, if not necessary.
            if (this.useCustomServiceTimeout && this.customServiceTimeout > SystemUtils.ServiceTimeoutDefault * 10)
            {
                this.infoField.Visibility = Visibility.Collapsed;
            }
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the image to be shown in the info field in the dialog.
        /// </summary>
        public static ImageSource InfoImage { get => SystemUtils.GetSystemIcon(SystemUtils.SystemIcon.Warning, SystemUtils.SystemIconSize.Small); }

        /// <summary>
        /// Gets a list of all services that are available for debugging.
        /// </summary>
        public ObservableCollection<ServiceModel> Services { get; } = new ObservableCollection<ServiceModel>();

        /// <summary>
        /// Gets or sets the custom service timeout in milliseconds.
        /// </summary>
        public int CustomServiceTimeout
        {
            get
            {
                return this.customServiceTimeout;
            }

            set
            {
                this.customServiceTimeout = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.CustomServiceTimeout)));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a custom service timeout should be used or not.
        /// </summary>
        public bool UseCustomServiceTimeout
        {
            get
            {
                return this.useCustomServiceTimeout;
            }

            set
            {
                this.useCustomServiceTimeout = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.UseCustomServiceTimeout)));
            }
        }

        /// <summary>
        /// Gets the command for setting the service timeout.
        /// </summary>
        public ICommand SetServiceTimeoutCommand
        {
            get
            {
                return this.setServiceTimeoutCommand ?? (this.setServiceTimeoutCommand = new DelegateCommand((x) => this.ExecuteSetServiceTimeout(), (x) => true));
            }
        }

        /// <summary>
        /// Gets the command for toggling the debugger.
        /// </summary>
        public ICommand ToggleDebuggerCommand
        {
            get
            {
                return this.toggleDebuggerCommand ?? (this.toggleDebuggerCommand = new DelegateCommand((x) => ExecuteToggleDebugger(x), (x) => true));
            }
        }

        private static IList<ServiceModel> GetServices()
        {
            var services = new List<ServiceModel>();
            ServiceController[] serviceControllers = ServiceController.GetServices();
            foreach (var serviceController in serviceControllers)
            {
                string imagePath = serviceController.GetImagePath();
                string fileName = executableFileNameRegEx.Match(imagePath).Value;

                // Only add services with a file name and that are not excluded.
                if (string.IsNullOrEmpty(fileName) || excludedServices.Contains(fileName.ToUpperInvariant()))
                {
                    continue;
                }

                ServiceModel service = new ServiceModel
                {
                    ImagePath = imagePath,
                    FileName = fileName,
                    DisplayName = $"{serviceController.DisplayName} ({fileName})",
                };

                try
                {
                    service.IsDebuggerSet = SystemUtils.IsIfeoDebuggerSet(fileName, Assembly.GetExecutingAssembly().Location);
                    services.Add(service);
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    // Just ignore exception (service will not be added to list).
                }
            }

            return services.OrderBy(x => x.DisplayName).ToList();
        }

        /// <summary>
        /// Toggle the debugger check box in the UI.
        /// </summary>
        /// <param name="parameter">The parameter from Command.Execute().</param>
        private static void ExecuteToggleDebugger(object parameter)
        {
            var serviceModel = parameter as ServiceModel;

            if (serviceModel == null)
            {
                return;
            }

            serviceModel.IsDebuggerSet = !serviceModel.IsDebuggerSet;
        }

        /// <summary>
        /// Sets the service timeout to the value set in the UI.
        /// </summary>
        private void ExecuteSetServiceTimeout()
        {
            if (!this.UseCustomServiceTimeout)
            {
                SystemUtils.SetDefaultServiceTimeout();
            }
            else
            {
                SystemUtils.SetServiceTimeout(this.CustomServiceTimeout);
            }

            MessageBoxResult result = MessageBox.Show(
                (string)Application.Current.FindResource("RestartForServiceTimeoutQuestion"),
                (string)Application.Current.FindResource("RestartForServiceTimeoutQuestionTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SystemUtils.Restart((string)Application.Current.FindResource("RestartForServiceTimeoutMessage"), new TimeSpan(0, 0, 5));
            }
        }

        /// <summary>
        /// Called when a service property is changed in the UI.
        /// </summary>
        /// <param name="sender">The service for which a property has changed.</param>
        /// <param name="e">The event arguments.</param>
        private void ServiceModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var service = sender as ServiceModel;
            if (service == null || e.PropertyName != nameof(ServiceModel.IsDebuggerSet))
            {
                return;
            }

            if (service.IsDebuggerSet)
            {
                var debuggerCommand = $"\"{Assembly.GetExecutingAssembly().Location}\" --Debug";
                SystemUtils.SetIfeoDebugger(service.FileName, debuggerCommand);
            }
            else
            {
                SystemUtils.RemoveIfeoDebugger(service.FileName);
            }
        }
    }
}
