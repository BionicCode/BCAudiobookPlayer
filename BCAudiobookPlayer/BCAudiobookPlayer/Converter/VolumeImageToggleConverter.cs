using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace BCAudiobookPlayer.Converter
{
  public class VolumeImageToggleConverter : DependencyObject, IValueConverter
  {
    public static readonly DependencyProperty MutedImageProperty = DependencyProperty.Register(
      "MutedImage",
      typeof(string),
      typeof(VolumeImageToggleConverter),
      new PropertyMetadata(default(string)));

    public string MutedImage { get { return (string) GetValue(VolumeImageToggleConverter.MutedImageProperty); } set { SetValue(VolumeImageToggleConverter.MutedImageProperty, value); } }

    public static readonly DependencyProperty LowVolumeImageProperty = DependencyProperty.Register(
      "LowVolumeImage",
      typeof(string),
      typeof(VolumeImageToggleConverter),
      new PropertyMetadata(default(string)));

    public string LowVolumeImage { get { return (string) GetValue(VolumeImageToggleConverter.LowVolumeImageProperty); } set { SetValue(VolumeImageToggleConverter.LowVolumeImageProperty, value); } }

    public static readonly DependencyProperty MediumVolumeImageProperty = DependencyProperty.Register(
      "MediumVolumeImage",
      typeof(string),
      typeof(VolumeImageToggleConverter),
      new PropertyMetadata(default(string)));

    public string MediumVolumeImage { get { return (string) GetValue(VolumeImageToggleConverter.MediumVolumeImageProperty); } set { SetValue(VolumeImageToggleConverter.MediumVolumeImageProperty, value); } }

    public static readonly DependencyProperty HighVolumeImageProperty = DependencyProperty.Register(
      "HighVolumeImage",
      typeof(string),
      typeof(VolumeImageToggleConverter),
      new PropertyMetadata(default(string)));

    public string HighVolumeImage { get { return (string) GetValue(VolumeImageToggleConverter.HighVolumeImageProperty); } set { SetValue(VolumeImageToggleConverter.HighVolumeImageProperty, value); } }

    public static readonly DependencyProperty MaxVolumeImageProperty = DependencyProperty.Register(
      "MaxVolumeImage",
      typeof(string),
      typeof(VolumeImageToggleConverter),
      new PropertyMetadata(default(string)));

    public string MaxVolumeImage { get { return (string) GetValue(VolumeImageToggleConverter.MaxVolumeImageProperty); } set { SetValue(VolumeImageToggleConverter.MaxVolumeImageProperty, value); } }

    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (!(value is double volume))
      {
        return value.ToString();
      }

      if (volume.Equals(0d))
      {
        return new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = this.MutedImage, IsHitTestVisible = false };
      }

      if (volume < 0.25)
      {
        return new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = this.LowVolumeImage, IsHitTestVisible = false };
      }

      if (volume < 0.5)
      {
        return new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = this.MediumVolumeImage, IsHitTestVisible = false };
      }

      if (volume < 0.75)
      {
        return new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = this.HighVolumeImage, IsHitTestVisible = false };
      }

      if (volume >= 0.75)
      {
        return new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = this.MaxVolumeImage, IsHitTestVisible = false };
      }

      return new FontIcon() { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = this.MaxVolumeImage, IsHitTestVisible = false };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotSupportedException();
    }

    #endregion

  }
}