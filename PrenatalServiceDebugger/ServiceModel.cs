// <copyright file="ServiceModel.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System.ComponentModel;

    /// <summary>
    /// Model for representing a service in the UI.
    /// </summary>
    public class ServiceModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Indicates whether a debugger should be used for this service or not.
        /// </summary>
        private bool isDebuggerSet;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the name of the service.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the file name of the service.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the image path of the service.
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a debugger should be used for this service or not.
        /// </summary>
        public bool IsDebuggerSet
        {
            get
            {
                return this.isDebuggerSet;
            }

            set
            {
                this.isDebuggerSet = value;
                this.RaisePropertyChanged("IsDebuggerSet");
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
    }
}
