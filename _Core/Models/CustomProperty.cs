using System;
using System.Reflection;

namespace Netkeiba.Models
{
	public class CustomProperty
	{
		public CustomProperty(PropertyInfo property, string name, Type type, FeaturesAttribute? attribute)
		{
			Property = property;
			Name = name;
			Type = type;
			Attribute = attribute;
		}

		public PropertyInfo Property { get; set; }
		public string Name { get; set; }
		public Type Type { get; set; }
		public FeaturesAttribute? Attribute { get; set; }
	}
}
