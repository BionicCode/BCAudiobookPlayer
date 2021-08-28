using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class ImageToggleConverter : DependencyObject, IValueConverter
  {
    public static readonly DependencyProperty PrimaryImageProperty = DependencyProperty.Register(
      "PrimaryImage",
      typeof(Symbol),
      typeof(ImageToggleConverter),
      new PropertyMetadata(default(Symbol)));

    public Symbol PrimaryImage { get { return (Symbol) GetValue(ImageToggleConverter.PrimaryImageProperty); } set { SetValue(ImageToggleConverter.PrimaryImageProperty, value); } }

    public static readonly DependencyProperty AlternativeImageProperty = DependencyProperty.Register(
      "AlternativeImage",
      typeof(Symbol),
      typeof(ImageToggleConverter),
      new PropertyMetadata(default(Symbol)));

    public Symbol AlternativeImage { get { return (Symbol) GetValue(ImageToggleConverter.AlternativeImageProperty); } set { SetValue(ImageToggleConverter.AlternativeImageProperty, value); } }

    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      return value is bool isTrue && isTrue ? new SymbolIcon(this.AlternativeImage) : new SymbolIcon(this.PrimaryImage);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      return value is Symbol image && image.Equals(this.AlternativeImage)
             || value is SymbolIcon symbolIcon && symbolIcon.Symbol.Equals(this.AlternativeImage);
    }

    #endregion

  }
}