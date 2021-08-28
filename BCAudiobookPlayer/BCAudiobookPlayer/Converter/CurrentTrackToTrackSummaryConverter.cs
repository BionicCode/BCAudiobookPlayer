using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using BCAudiobookPlayer.Player.Playback.Contract;
using JetBrains.Annotations;

namespace BCAudiobookPlayer.Converter
{
  public class CurrentTrackToTrackSummaryConverter : DependencyObject, IValueConverter
  {
    #region Parameter attached property

    public static readonly DependencyProperty AudiobookProperty = DependencyProperty.RegisterAttached(
      "Audiobook", typeof(IAudiobook), typeof(CurrentTrackToTrackSummaryConverter), new PropertyMetadata(default(IAudiobook), CurrentTrackToTrackSummaryConverter.OnAttached));

    private static void OnAttached(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if (e.NewValue is IAudiobook audiobook)
      {
        if (!CurrentTrackToTrackSummaryConverter.Audiobooks.Contains(audiobook))
        {
          CurrentTrackToTrackSummaryConverter.Audiobooks.Add(audiobook);
        }
      }
    }

    public static void SetAudiobook([NotNull] DependencyObject attachingElement, IAudiobook value)
    {
      attachingElement.SetValue(CurrentTrackToTrackSummaryConverter.AudiobookProperty, value);
    }

    public static IAudiobook GetAudiobook([NotNull] DependencyObject attachingElement)
    {
      return (IAudiobook) attachingElement.GetValue(CurrentTrackToTrackSummaryConverter.AudiobookProperty);
    }

    #endregion

    static CurrentTrackToTrackSummaryConverter()
    {
      CurrentTrackToTrackSummaryConverter.Audiobooks = new List<IAudiobook>();
      CurrentTrackToTrackSummaryConverter.ParameterTable = new Dictionary<IAudiobookPart, IAudiobook>();
    }

    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, string language)
    {
      if (value is IAudiobook audiobookArg)
      {
        return $"Playing Track #{audiobookArg.CurrentPartIndex + 1} of {audiobookArg.PartCount}";
      }

      if (!(value is IAudiobookPart audioFile))
      {
        return value?.ToString();
      }

      if (!CurrentTrackToTrackSummaryConverter.ParameterTable.TryGetValue(audioFile, out IAudiobook currentPartAudiobook))
      {
        currentPartAudiobook = CurrentTrackToTrackSummaryConverter.Audiobooks.FirstOrDefault(
          (audiobook) => audiobook.CurrentPart.Equals(audioFile));
        CurrentTrackToTrackSummaryConverter.ParameterTable.Add(audioFile, currentPartAudiobook);
      }

      return currentPartAudiobook != null ? $"Playing Track #{currentPartAudiobook.CurrentPartIndex + 1} of {currentPartAudiobook.PartCount}" : value.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
      throw new NotSupportedException();
    }

    #endregion
    private static List<IAudiobook> Audiobooks { get; set; }
    private static Dictionary<IAudiobookPart, IAudiobook> ParameterTable { get; set; }
  }
}