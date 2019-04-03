using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace MLDotNetUWP
{
    /// <summary>
    /// The Windows Community Toolkit extension ensures we only get numeric values from the textboxes.
    /// </summary>
    class FloatToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((float)value).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return float.Parse(value as string);
        }

    }
}
