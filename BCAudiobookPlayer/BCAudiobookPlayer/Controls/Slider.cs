using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Shapes;

namespace BCAudiobookPlayer.Controls
{
    public class Slider : Windows.UI.Xaml.Controls.Slider
    {
      public event EventHandler SliderDragCompleted;
      private Thumb ThumbControl { get; set; }
      private Rectangle TrackControl { get; set; }
      private DispatcherTimer DelayTimer { get; }

      public Slider()
      {
        this.Loaded += Initialize;
      }

      private void Initialize(object sender, RoutedEventArgs e)
      {
        this.ValueBindingBackup = GetBindingExpression(RangeBase.ValueProperty)?.ParentBinding;
    }

      protected override void OnApplyTemplate()
      {
        base.OnApplyTemplate();

        this.ThumbControl = GetTemplateChild("HorizontalThumb") as Thumb;
        this.ThumbControl.DragCompleted += OnDragCompleted;
        this.ThumbControl.DragStarted += OnDragStarted;


        this.TrackControl = GetTemplateChild("HorizontalTrackRect") as Rectangle;
        this.TrackControl.IsHitTestVisible = true;
        this.TrackControl.PointerPressed += OnMouseDown;
        this.TrackControl.PointerReleased += OnMouseUp;
      }

      private void OnDragStarted(object sender, DragStartedEventArgs e)
      {
      ClearValueBinding();
    }

      private void OnDragCompleted(object sender, DragCompletedEventArgs e)
      {
        OnSliderDragCompleted();
      }

      private void OnMouseDown(object sender, PointerRoutedEventArgs e)
      {
      }

      private void OnMouseUp(object sender, PointerRoutedEventArgs e)
    {
      OnSliderDragCompleted();
    }

      #region Overrides of Control

      /// <inheritdoc />
      protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
      {
        base.OnPointerWheelChanged(e);
      ClearValueBinding();
      this.Value += e.GetCurrentPoint(this).Properties.MouseWheelDelta / (120.0 * 100.0) * 3;
        OnSliderDragCompleted();
    }

      /// <inheritdoc />
      protected override void OnTapped(TappedRoutedEventArgs e)
      {
        base.OnTapped(e);
        OnSliderDragCompleted();
      }

    //  /// <inheritdoc />
    //  protected override void OnPointerReleased(PointerRoutedEventArgs e)
    //  {
    //    base.OnPointerReleased(e);
    //    OnSliderDragCompleted();
    //}

      #endregion

      protected virtual void OnSliderDragCompleted()
    {
      this.SliderDragCompleted?.Invoke(this, EventArgs.Empty);
      TryRestoreValueBinding();
      }

      private void ClearValueBinding()
    {
      this.ValueBindingBackup = GetBindingExpression(RangeBase.ValueProperty)?.ParentBinding;
      var currentValue = this.Value;
      SetBinding(RangeBase.ValueProperty, new Binding());
      this.Value = currentValue;
    }

      private bool TryRestoreValueBinding()
      {
        if (this.ValueBindingBackup != null)
        {
          SetBinding(RangeBase.ValueProperty, this.ValueBindingBackup);
        }

        return this.ValueBindingBackup != null;
      }

      public Binding ValueBindingBackup { get; set; }
    }
}
