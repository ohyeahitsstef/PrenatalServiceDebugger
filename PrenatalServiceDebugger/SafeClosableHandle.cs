// <copyright file="SafeClosableHandle.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Class for closing native Win32 handles.
    /// </summary>
    internal sealed class SafeClosableHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeClosableHandle"/> class.
        /// </summary>
        /// <param name="handle">The native Win32 handle, that can be closed with <see cref="NativeMethods.CloseHandle(IntPtr)"/>.</param>
        public SafeClosableHandle(IntPtr handle)
            : base(true)
        {
            this.SetHandle(handle);
        }

        /// <summary>
        /// Closes the native Win32 handle.
        /// </summary>
        /// <returns>Returns true if the handle was successfully closed, otherwise false is returned.</returns>
        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(this.handle);
        }
    }
}
