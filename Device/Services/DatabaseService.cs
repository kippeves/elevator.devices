using System.Data;
using System.Data.SqlClient;
using Dapper;

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
    public async Task UpdateLogWithEvent(Guid ElevatorId, string Description, string EventType, bool EventResult)
    {
        try{
            using IDbConnection conn = new SqlConnection(_connectionString);
            
            //fetch ID for EventType, Create a new entry if it does not exist
            //log EventType with new info

            var EventTypeId = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Id FROM EventType WHERE EventType.Name = @EventType",
                new { EventType = EventType}
            );

            if(string.IsNullOrEmpty(EventTypeId))
            {
                await conn.ExecuteAsync(
                    "INSERT INTO EventType (Name) VALUES (@EventType)",
                    new {EventType = EventType}
                );

                EventTypeId = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT Id FROM EventType WHERE EventType.Name = @EventType",
                    new { EventType = EventType}
                );
            }

            await conn.ExecuteAsync(
                "INSERT INTO ElevatorLog (ElevatorId, TimeStamp, LogDescription, EventTypeId, EventResult) VALUES (@ElevatorId, @TimeStamp, @LogDescription, @EventTypeId, @EventResult)",
                new { ElevatorId = ElevatorId, TimeStamp = DateTime.Now, LogDescription = Description, EventTypeId = EventTypeId, EventResult = EventResult}
            );
        }
        catch{}
    }
}