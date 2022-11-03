using System.Data;
using System.Data.SqlClient;
using Dapper;
using Device.Interfaces;
using Device.Models;

namespace Device.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> UpdateElevatorMetaInfo(Guid ElevatorId, string Key, dynamic value)
    {
        try{
            using IDbConnection conn = new SqlConnection(_connectionString);

            var remote_key = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT key value FROM ElevatorMetaInformation WHERE ElevatorMetaInformation.ElevatorId = @ElevatorId AND ElevatorMetaInformation.key = @key",
                new {ElevatorId = ElevatorId, key = Key}
                );
            
            if(string.IsNullOrEmpty(remote_key))
            {
                await conn.QueryAsync(
                    "INSERT INTO ElevatorMetaInformation VALUES (@ElevatorId, @key, @Value)",
                    new {ElevatorId = ElevatorId, key = Key, Value = value}
                    );
                return true;
            }
            else
            {
                await conn.QueryAsync(
                    "UPDATE ElevatorMetaInformation SET value = @Value WHERE  ElevatorMetaInformation.ElevatorId = @ElevatorId AND ElevatorMetaInformation.key = @key",
                    new {Value = value, ElevatorId = ElevatorId, key = Key}
                    ); 
                return true;
            }
        }
        catch{}
        return false;
    }
    public async Task<bool> UpdateLogWithEvent(List<ElevatorLog> list)
    {
        var updateQuery = "INSERT INTO ElevatorLog (ElevatorId, TimeStamp, LogDescription, EventTypeId, EventResult) VALUES";
        foreach(var listItem in list)
        {
            updateQuery += $"({listItem.ElevatorId},{listItem.TimeStamp},{listItem.Description},{listItem.EventTypeId},{listItem.EventResult})";
        }
        try{
            using IDbConnection conn = new SqlConnection(_connectionString);
            await conn.QueryAsync(updateQuery);
            return true;
        }
        catch{

        }
        return false;
    }
}
