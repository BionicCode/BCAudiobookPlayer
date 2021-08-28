using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCAudiobookPlayer.Player
{
  public class ValueChangedEventArgs<TValue> : EventArgs
  {
    public ValueChangedEventArgs(TValue value)
    {
      this.Value = value;
    }

    public TValue Value { get; set; }
  }
}
