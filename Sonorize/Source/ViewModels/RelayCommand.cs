using System;
using System.Windows.Input;

namespace Sonorize.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;
    private EventHandler? _canExecuteChanged;

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            // Note: Avalonia doesn't have a global CommandManager.RequerySuggested like WPF.
            // For simplicity, this basic RelayCommand requires manual raising of CanExecuteChanged
            // or relies on UI controls that re-evaluate CanExecute on interactions.
            // For more robust scenarios, integrate with a mechanism that allows VMs to signal changes.
            _canExecuteChanged += value;
        }
        remove
        {
            _canExecuteChanged -= value;
        }
    }

    public void RaiseCanExecuteChanged()
    {
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(o => execute(), canExecute == null ? (Predicate<object?>?)null : o => canExecute())
    {
    }


    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
    public void Execute(object? parameter) => _execute(parameter);
}