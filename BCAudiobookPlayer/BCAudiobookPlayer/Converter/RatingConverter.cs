#region Info
// //  
// BCAudiobookPlayer
#endregion

using System;
using Windows.UI.Xaml.Data;

namespace BCAudiobookPlayer.Converter
{
  public class RatingConverter : IValueConverter
  {
    private const double UnitRatio = 99.0 / 5.0;
    private const string Unit = "kbps";
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      var rating = (uint) value;
      var starRating = rating == 0 ? 0 : Math.Round(rating / 25.0) + 1;
      return starRating;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      var starRating = (double) value;
      var rating = starRating.Equals(0) ? 0 : (uint) Math.Round((starRating - 1) * 25);
      return rating;
    }

    #endregion
  }
}