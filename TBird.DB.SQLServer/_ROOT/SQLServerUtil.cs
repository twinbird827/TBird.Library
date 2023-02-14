using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace TBird.DB.SQLServer
{
    public static class SQLServerUtil
    {
        public static SqlParameter CreateParameter(DbType type, object value)
        {
            return new SqlParameter()
            {
                DbType = type,
                Value = value
            };
        }
    }
}
