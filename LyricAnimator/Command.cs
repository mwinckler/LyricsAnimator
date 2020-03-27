using System;
using System.Windows.Input;

namespace LyricAnimator
{
    internal sealed class Command : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Action execute;

        public Command(Action execute)
        {
            this.execute = execute;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter) => execute();
    }
}