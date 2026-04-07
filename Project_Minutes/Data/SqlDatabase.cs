using Microsoft.Data.SqlClient;
using Project_Minutes.Configuration;

namespace Project_Minutes.Data;

public sealed class SqlDatabase(AppConfiguration config)
{
    public SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(config.MeetingMinutesConnectionString);
        return connection;
    }

    public int CommandTimeoutSeconds => config.CommandTimeoutSeconds;
}
