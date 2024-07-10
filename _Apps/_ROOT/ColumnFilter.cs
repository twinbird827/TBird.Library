using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public class ColumnFilter
	{
		public ColumnFilter ()
		{
			Key = string.Empty;
			Value = new string[] { }; 
		}

		public string Key { get; set; }

		public string[] Value { get; set; }
	}
}
