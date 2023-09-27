using System.Data.Common;
using System.Threading.Tasks;

namespace TBird.DB
{
	public static class DbControlExtension
	{
		public static async Task<T> ExecuteScalarAsync<T>(this DbControl command, string sql, params DbParameter[] parameters)
		{
			return DbUtil.GetValue<T>(await command.ExecuteScalarAsync(sql, parameters));
		}
	}
}