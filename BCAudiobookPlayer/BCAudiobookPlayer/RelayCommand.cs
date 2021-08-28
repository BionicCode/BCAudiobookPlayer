using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BCAudiobookPlayer
{
  /// <summary>
  /// An implementation independent ICommand implementation.
  /// Enables instant creation of an ICommand without implementing the ICommand interface for each command.
  /// The individual Execute() an CanExecute() members are suplied via delegates.
  /// <seealso cref="System.Windows.Input.ICommand"/>
  /// </summary>
  /// <remarks>The type of <c>RelaisCommand</c> actually is a <see cref="System.Windows.Input.ICommand"/></remarks>
    public class RelayCommand : ICommand
    {
    private readonly Func<object, Task> _executeAsync;
    private readonly Action<object> _execute;
    private readonly Predicate<object> _canExecute;
    /// <summary>
    /// Raised when RaiseCanExecuteChanged is called.
    /// </summary>
    public event EventHandler CanExecuteChanged;
    /// <summary>
    /// Creates a new command that can always execute.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    public RelayCommand(Action<object> execute)
        : this(execute, null)
    {
    }
    /// <summary>
    /// Creates a new command that can always execute.
    /// </summary>
    /// <param name="executeAsync">The awaitable execution logic.</param>
    public RelayCommand(Func<object, Task> executeAsync)
        : this(executeAsync, null)
    {
    }
    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action<object> execute, Predicate<object> canExecute)
    {
      this._execute = execute ?? throw new ArgumentNullException(nameof(execute));
      this._canExecute = canExecute;
    }
    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="executeAsync">The awaitable execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute)
    {
      this._executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
      this._canExecute = canExecute;
    }
    /// <summary>
    /// Determines whether this RelayCommand can execute in its current state.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. If the command does not require data to be passed, 
    /// this object can be set to null.
    /// </param>
    /// <returns>true if this command can be executed; otherwise, false.</returns>
    public bool CanExecute(object parameter)
    {
      return this._canExecute == null || this._canExecute(parameter);
    }
    /// <summary>
    /// Executes the RelayCommand on the current command target.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. If the command does not require data to be passed, 
    /// this object can be set to null.
    /// </param>
    async void ICommand.Execute(object parameter)
    {
      if (this._executeAsync != null)
      {
        await ExecuteAsync(parameter);
        return;
      }
      this._execute(parameter);
    }

    /// <summary>
    /// Executes the RelayCommand on the current command target.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. If the command does not require data to be passed, 
    /// this object can be set to null.
    /// </param>
    public async Task ExecuteAsync(object parameter)
    {
      if (this._executeAsync != null)
      {
        await this._executeAsync(parameter);
        return;
      }
      this._execute(parameter);
    }
    /// <summary>
    /// Method used to raise the CanExecuteChanged event
    /// to indicate that the return value of the CanExecute
    /// method has changed.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
      var handler = this.CanExecuteChanged;
      if (handler != null)
      {
        handler(this, EventArgs.Empty);
      }
    }
  }
  /// <summary>
  /// An implementation independent ICommand implementation.
  /// Enables instant creation of an ICommand without implementing the ICommand interface for each command.
  /// The individual Execute() an CanExecute() members are suplied via delegates.
  /// <seealso cref="System.Windows.Input.ICommand"/>
  /// </summary>
  /// <remarks>The type of <c>RelaisCommand</c> actually is a <see cref="System.Windows.Input.ICommand"/></remarks>
  public class RelayCommand<TParam> : RelayCommand
  {
    /// <summary>
    /// Creates a new command that can always execute.
    /// </summary>
    /// <param name="executeAsync">The awaitable execution logic.</param>
    public RelayCommand(Func<TParam, Task> executeAsync)
        : this(executeAsync, null)
    {
    }
    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action<TParam> execute, Predicate<TParam> canExecute) : base((param) => execute((TParam) param), (param) => canExecute((TParam) param))
    {
    }
    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="executeAsync">The awaitable execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Func<TParam, Task> executeAsync, Predicate<TParam> canExecute) : base((param) => executeAsync((TParam) param), (param) => canExecute((TParam) param))
    {
    }
    /// <summary>
    /// Determines whether this RelayCommand can execute in its current state.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. If the command does not require data to be passed, 
    /// this object can be set to null.
    /// </param>
    /// <returns>true if this command can be executed; otherwise, false.</returns>
    public bool CanExecute(TParam parameter)
    {
      return base.CanExecute(parameter);
    }
    /// <summary>
    /// Executes the RelayCommand on the current command target.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. If the command does not require data to be passed, 
    /// this object can be set to null.
    /// </param>
    public void Execute(TParam parameter)
    {
      ((ICommand) this).Execute(parameter);
    }
    /// <summary>
    /// Executes the RelayCommand on the current command target.
    /// </summary>
    /// <param name="parameter">
    /// Data used by the command. If the command does not require data to be passed, 
    /// this object can be set to null.
    /// </param>
    public async Task ExecuteAsync(TParam parameter)
    {
      await base.ExecuteAsync(parameter);
    }
  }
}
