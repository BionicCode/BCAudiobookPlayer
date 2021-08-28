using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace BCAudiobookPlayer.Converter
{
  public class BooleanToColorConverter : DependencyObject, IValueConverter
  {
    public static readonly DependencyProperty TrueStateBrushProperty = DependencyProperty.Register(
      "TrueStateBrush",
      typeof(SolidColorBrush),
      typeof(BooleanToColorConverter),
      new PropertyMetadata(default(SolidColorBrush)));

    public SolidColorBrush TrueStateBrush { get { return (SolidColorBrush) GetValue(BooleanToColorConverter.TrueStateBrushProperty); } set { SetValue(BooleanToColorConverter.TrueStateBrushProperty, value); } }

    public static readonly DependencyProperty FalseStateBrushProperty = DependencyProperty.Register(
      "FalseStateBrush",
      typeof(SolidColorBrush),
      typeof(BooleanToColorConverter),
      new PropertyMetadata(default(SolidColorBrush)));

    public SolidColorBrush FalseStateBrush { get { return (SolidColorBrush) GetValue(BooleanToColorConverter.FalseStateBrushProperty); } set { SetValue(BooleanToColorConverter.FalseStateBrushProperty, value); } }

    public static readonly DependencyProperty IndeterminateStateBrushProperty = DependencyProperty.Register(
      "IndeterminateStateBrush",
      typeof(SolidColorBrush),
      typeof(BooleanToColorConverter),
      new PropertyMetadata(default(SolidColorBrush)));

    public SolidColorBrush IndeterminateStateBrush { get { return (SolidColorBrush) GetValue(BooleanToColorConverter.IndeterminateStateBrushProperty); } set { SetValue(BooleanToColorConverter.IndeterminateStateBrushProperty, value); } }

    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value is bool isVisible)
      {
        return isVisible 
          ? this.TrueStateBrush 
          : this.FalseStateBrush;
      }
      return this.IndeterminateStateBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      if (value is SolidColorBrush brush)
      {
        return brush.Color.Equals(this.TrueStateBrush.Color) ? (bool?) true : brush.Color.Equals(this.FalseStateBrush.Color) ? (bool?) false : null;
      }

      return value;
    }

    #endregion
  }
}