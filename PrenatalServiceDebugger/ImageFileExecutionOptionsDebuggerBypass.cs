// <copyright file="ImageFileExecutionOptionsDebuggerBypass.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.IO;

    /// <summary>
    /// Class to bypass IFEO Debugger for an executable.
    /// </summary>
    internal sealed class ImageFileExecutionOptionsDebuggerBypass : IDisposable
    {
        private readonly string debugger;
        private readonly string executableName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFileExecutionOptionsDebuggerBypass"/> class.
        /// </summary>
        /// <param name="executableName">The name of the executable for which to bypass the IFEO debugger.</param>
        public ImageFileExecutionOptionsDebuggerBypass(string executableName)
        {
            this.executableName = Path.GetFileName(executableName);
            this.debugger = SystemUtils.GetIfeoDebugger(executableName);
            SystemUtils.RemoveIfeoDebugger(executableName);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.debugger == null)
            {
                return;
            }

            SystemUtils.SetIfeoDebugger(this.executableName, this.debugger);
        }
    }
}
