// <copyright file="ServiceControllerExtensions.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.CodeDom;
    using System.ServiceProcess;
    using Microsoft.Win32;

    /// <summary>
    /// Extension methods for <see cref="ServiceController"/>.
    /// </summary>
    public static class ServiceControllerExtensions
    {
        /// <summary>
        /// Gets the image path of the service.
        /// </summary>
        /// <param name="serviceController">The <see cref="ServiceController"/> instance.</param>
        /// <returns>Returns the image path or an empty string.</returns>
        public static string GetImagePath(this ServiceController serviceController)
        {
            if (serviceController == null)
            {
                throw new ArgumentNullException(nameof(serviceController));
            }

            string registryPath = $@"SYSTEM\CurrentControlSet\Services\{serviceController.ServiceName}";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                return key?.GetValue("ImagePath") as string ?? string.Empty;
            }
        }
    }
}
