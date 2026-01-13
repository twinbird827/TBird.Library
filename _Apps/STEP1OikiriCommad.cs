using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public class STEP1OikiriCommad : STEPBase
	{
		public STEP1OikiriCommad(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				await conn.CreateOikiri();

				var retry = false;
				do
				{
					foreach (var raceid in await conn.GetOikiriTargets().ToArrayAsync())
					{
						await conn.BeginTransaction();
						await conn.InsertOikiriAsync(raceid);
						conn.Commit();
						retry = true;
					}

					foreach (var uma in await conn.GetUmaTargets().ToArrayAsync())
					{
						await conn.BeginTransaction();
						await conn.InsertUmaInfoAsync(uma, string.Empty);
						conn.Commit();
						retry = true;
					}
				} while (retry);
			}
		}
	}
}