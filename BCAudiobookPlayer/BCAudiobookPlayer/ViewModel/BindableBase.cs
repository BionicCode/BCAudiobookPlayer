using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.ViewModel
{
  public abstract class BindableBase : INotifyPropertyChanged
  {
    protected bool TrySetPropertyValue<TValue>(
      ref TValue propertyBackingField,
      TValue newValue,
      bool forceUpdate = false,
      [CallerMemberName] string propertyName = null)
    {
      if (!forceUpdate && object.Equals(propertyBackingField, newValue))
      {
        return false;
      }

      propertyBackingField = newValue;
      OnPropertyChanged(propertyName);
      return true;
    }
    
    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}