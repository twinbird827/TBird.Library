using System.Collections.Generic;
using System.Linq;
using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Core
{
	public static class ComboUtil
	{
		private const string NicoComboPath = @"lib\nico-combo-setting.xml";
		private const string TubeComboPath = @"lib\tube-combo-setting.xml";
		private const string ViewComboPath = @"lib\view-combo-setting.xml";

		public static ComboboxModel[] Nicos { get; private set; }
		public static ComboboxModel[] Tubes { get; private set; }
		public static ComboboxModel[] Views { get; private set; }

		public static void Initialize()
		{
			Nicos = GetCombos(NicoComboPath);
			Tubes = GetCombos(TubeComboPath);
			Views = GetCombos(ViewComboPath);
		}

		private static ComboboxModel[] GetCombos(string path)
		{
			return XmlUtil.Load(path)
				.Descendants("combo")
				.Select(x => new ComboboxModel(
					x.AttributeS("group"),
					x.Descendants("item").Select(i =>
						new ComboboxItemModel(i.AttributeS("value"), i.AttributeS("display"))
					)
				))
				.ToArray();
		}

		public static IEnumerable<ComboboxItemModel> GetNicos(string group)
		{
			return Nicos.GetGroups(group);
		}

		public static string GetNicoDisplay(string group, string value)
		{
			return Nicos.GetDisplay(group, value);
		}

		public static IEnumerable<ComboboxItemModel> GetTubes(string group)
		{
			return Tubes.GetGroups(group);
		}

		public static string GetTubeDisplay(string group, string value)
		{
			return Tubes.GetDisplay(group, value);
		}

		public static IEnumerable<ComboboxItemModel> GetViews(string group)
		{
			return Views.GetGroups(group);
		}

		public static string GetViewDisplay(string group, string value)
		{
			return Views.GetDisplay(group, value);
		}

	}

	public static class ComboUtilExtension
	{
		public static IEnumerable<ComboboxItemModel> GetGroups(this ComboboxModel[] arr, string group)
		{
			return arr.Where(x => x.Group == group).SelectMany(x => x.Items);
		}

		public static string GetDisplay(this ComboboxModel[] arr, string group, string value)
		{
			return arr.GetGroups(group).FirstOrDefault(x => x.Value == value)?.Display;
		}
	}
}