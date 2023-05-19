using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;
using TBird.Core;

namespace TBird.DB.SQLServer
{
    public class SQLServerControl : DbControl
    {
        public SQLServerControl(string connectionString) : base(connectionString)
        {

        }

        public override DbConnection CreateConnection(string connectionString)
        {
            var dic = ToConnectionDictionary(connectionString);
            var builder = new SqlConnectionStringBuilder()
            {
                DataSource = dic["datasource"],
                UserID = dic.Get("userid"),
                Password = dic.Get("password"),
                InitialCatalog = dic.Get("initialcatalog", "master"),
                ConnectTimeout = int.Parse(dic.Get("connecttimeout", "15000")),
            };

            return new SqlConnection(builder.ToString());
        }
    }
}
