﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace TBird.Wpf.Converters
{
    public class Null2BooleanConverter : IValueConverter
    {
        private static Dictionary<Type, object> _structdefaults = new Dictionary<Type, object>()
        {
            { typeof(short), default(short) },
            { typeof(int), default(int) },
            { typeof(long), default(long) },
            { typeof(float), default(float) },
            { typeof(double), default(double) },
            { typeof(DateTime), default(DateTime) },
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (_structdefaults.ContainsKey(targetType))
            {
                return value != _structdefaults[targetType];
            }
            else
            {
                return value != null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}