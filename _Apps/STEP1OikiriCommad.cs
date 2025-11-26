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

				foreach (var raceid in await conn.GetOikiriTargets().ToArrayAsync())
				{
					await conn.BeginTransaction();
					await conn.InsertOikiriAsync(raceid);
					conn.Commit();
				}

				foreach (var uma in await conn.GetUmaTargets().ToArrayAsync())
				{
					await conn.BeginTransaction();
					await conn.InsertUmaInfoAsync(uma, string.Empty);
					conn.Commit();
				}
			}
		}
	}

	public static partial class SQLite3Extensions
	{
		public static async Task DropSTEP1Oikiri(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_oikiri");
		}

		public static async Task CreateOikiri(this SQLiteControl conn)
		{
			if (await conn.ExistsColumn("t_oikiri", "ﾚｰｽID")) return;

			await conn.Create(
				"t_oikiri",
				Arr("ﾚｰｽID", "枠番", "馬番", "馬ID", "コース", "馬場", "乗り役", "時間1", "時間2", "時間3", "時間4", "時間5", "時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "一言", "評価"),
				Arr("ﾚｰｽID", "馬ID")
			);
		}

		public static async IAsyncEnumerable<string> GetOikiriTargets(this SQLiteControl conn)
		{
			var sql = @$"
SELECT ﾚｰｽID FROM t_orig_h WHERE ﾚｰｽID NOT IN (SELECT ﾚｰｽID FROM t_oikiri)
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["ﾚｰｽID"].Str();
			}
		}

		public static async Task InsertOikiriAsync(this SQLiteControl conn, string raceid)
		{
			if (!await conn.Exists("t_oikiri", "ﾚｰｽID", raceid))
			{
				var racearr = await NetkeibaGetter.GetOikiris(raceid);
				foreach (var x in racearr)
				{
					await conn.InsertAsync("t_oikiri", x);
				}
			}
		}

		public static async IAsyncEnumerable<string> GetUmaTargets(this SQLiteControl conn)
		{
			var sql = @$"
SELECT DISTINCT 馬ID FROM (SELECT 父ID 馬ID FROM t_uma UNION ALL SELECT 母父ID 馬ID FROM t_uma) WHERE 馬ID NOT IN (SELECT 馬ID FROM t_uma) AND 馬ID <> '' AND 馬ID IS NOT NULL ORDER BY 馬ID
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["馬ID"].Str();
			}
		}

	}

}