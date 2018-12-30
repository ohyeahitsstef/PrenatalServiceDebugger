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
    using System.ServiceProcess;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
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
        private static List<string> excludedServices = new List<string> { "svchost.exe" };

        /// <summary>
        /// Custom service timeout in milliseconds.
        /// </summary>
        private int customServiceTimeout = 0;

        /// <summary>
        /// Indicates whether a custom service timeout is used or not.
        /// </summary>
        private bool useCustomServiceTimeout;

        /// <summary>
        /// The command for setting the service timeout.
        /// </summary>
        private ICommand setServiceTimeoutCommand;

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
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

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
                this.RaisePropertyChanged("CustomTimeout");
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a custom service timout should be used or not.
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
                this.RaisePropertyChanged("UseCustomTimeout");
            }
        }

        /// <summary>
        /// Gets the command for setting the service timeout.
        /// </summary>
        public ICommand SetServiceTimeoutCommand
        {
            get
            {
                return this.setServiceTimeoutCommand ?? (this.setServiceTimeoutCommand = new DelegateCommand(() => this.ExecuteSetServiceTimeout(), () => true));
            }
        }

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                if (string.IsNullOrEmpty(fileName) || excludedServices.Contains(fileName.ToLower()))
                {
                    continue;
                }

                ServiceModel service = new ServiceModel
                {
                    ImagePath = imagePath,
                    FileName = fileName,
                    DisplayName = $"{serviceController.DisplayName} ({fileName})",
                    IsDebuggerSet = SystemUtils.IsIfeoDebuggerSet(fileName, Assembly.GetExecutingAssembly().Location)
                };

                services.Add(service);
            }

            return services.OrderBy(x => x.DisplayName).ToList();
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
#pragma warning disable IDE0019 // Use pattern matching: Results in a false-positive for a StyleCop rule.
            var service = sender as ServiceModel;
#pragma warning restore IDE0019 // Use pattern matching
            if (service == null || e.PropertyName != "IsDebuggerSet")
            {
                return;
            }

            if (service.IsDebuggerSet)
            {
#pragma warning disable SA1012 // Opening braces must be spaced correctly; False-positive: String interpolation is not correctly recognized.
                var debuggerCommand = $"\"{ Assembly.GetExecutingAssembly().Location}\" --Debug";
#pragma warning restore SA1012 // Opening braces must be spaced correctly
                SystemUtils.SetIfeoDebugger(service.FileName, debuggerCommand);
            }
            else
            {
                SystemUtils.RemoveIfeoDebugger(service.FileName);
            }
        }
    }
}
