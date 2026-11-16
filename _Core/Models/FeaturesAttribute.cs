using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	[AttributeUsage(AttributeTargets.Property)]
	public class FeaturesAttribute : Attribute
	{
		public FeaturesAttribute()
		{
			Normalization = false;
		}

		public bool Normalization { get; set; }
	}
}