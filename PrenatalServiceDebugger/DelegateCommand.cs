// <copyright file="DelegateCommand.cs" company="-">
// Copyright (c) Stefan Ortner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace PrenatalServiceDebugger
{
    using System;
    using System.Windows.Input;

    /// <summary>
    /// Helper class for generic delegate commands.
    /// </summary>
    internal class DelegateCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="execute">The action that should be performed when executing the command.</param>
        /// <param name="canExecute">The functor that decides whether the command can be executed or not.</param>
        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute)
            {
                this.execute = execute;
                this.canExecute = canExecute;
            }

        /// <inheritdoc/>
        public event EventHandler CanExecuteChanged;

        /// <inheritdoc/>
        public bool CanExecute(object parameter)
        {
            return this.canExecute(parameter);
        }

        /// <inheritdoc/>
        public void Execute(object parameter)
        {
            this.execute(parameter);
        }

        /// <summary>
        /// Raises <see cref="CanExecuteChanged"/> event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            this.CanExecuteChanged?.Invoke(this, null);
        }
    }
}
