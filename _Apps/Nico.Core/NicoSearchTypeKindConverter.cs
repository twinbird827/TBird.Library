using MahApps.Metro.IconPacks;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Moviewer.Nico.Core
{
	public class NicoSearchTypeKindConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is NicoSearchType t)
			{
				switch (t)
				{
					case NicoSearchType.User:
						return PackIconMaterialKind.AccountSearch;
					case NicoSearchType.Mylist:
						return PackIconMaterialKind.Star;
					case NicoSearchType.Tag:
						return PackIconMaterialKind.Tag;
					//case NicoSearchType.Word:
					default:
						return PackIconMaterialKind.Magnify;
				}
			}
			else
			{
				return PackIconMaterialKind.None;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}