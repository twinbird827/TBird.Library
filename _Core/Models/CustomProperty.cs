using System;
using System.Data;
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

		public string GetTypeString() => Type.Name switch
		{
			"Single" => "REAL",
			"UInt32" => "INTEGER",
			"Int32" => "INTEGER",
			"Boolean" => "INTEGER",
			_ => "TEXT"
		};

		public DbType GetDBType() => Type.Name switch
		{
			"Single" => DbType.Single,
			"UInt32" => DbType.Int32,
			"Int32" => DbType.Int32,
			"Boolean" => DbType.Int32,
			_ => DbType.String
		};

		public void SetProperty(OptimizedHorseFeatures instance, Dictionary<string, object> x)
		{
			if (x.TryGetValue(Name, out var value))
			{
				Property.SetValue(instance, Convert.ChangeType(value, Type));
			}
		}
	}
}
