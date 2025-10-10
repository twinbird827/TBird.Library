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

				await foreach (var raceid in conn.GetOikiriTargets())
				{
					await conn.BeginTransaction();
					await conn.InsertOikiriAsync(await NetkeibaGetter.GetOikiris(raceid));
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
SELECT ﾚｰｽID FROM t_orig_h WHERE 開催日 > IFNULL((SELECT 開催日 FROM t_orig_h WHERE ﾚｰｽID = (SELECT MAX(ﾚｰｽID) FROM t_oikiri)), '1900/01/01') ORDER BY 開催日 ASC, ﾚｰｽID ASC
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["ﾚｰｽID"].Str();
			}
		}

		public static async Task InsertOikiriAsync(this SQLiteControl conn, List<Dictionary<string, string>> racearr)
		{
			foreach (var x in racearr)
			{
				await conn.InsertAsync("t_oikiri", x);
			}
		}

	}

}