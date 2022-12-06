using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Json;
using Dapper;
using Device.Interfaces;
using Device.Models;

namespace Device.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;
    protected const string ConnectUrl = "https://kyhelevator.azurewebsites.net/api/Connect";

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(bool status, string message, DeviceInfo? data)> GetElevatorByIdAsync(Guid deviceId)
    {
        try
        {
            using IDbConnection conn = new SqlConnection(_connectionString);
            var device = (await conn.QueryAsync("SELECT [DeviceId],[Building].[BuildingName],[BuildingId], [CompanyId], [ElevatorTypeId], [IsFunctioning], [Name], [CompanyName], [ElevatorType], [NumberOfFloors] from ElevatorWithInfo, Building where DeviceId = @ElevatorId AND Building.Id = ElevatorWithInfo.BuildingId;",
                new { ElevatorId =  deviceId})).Select(row => new DeviceInfo()
            {
                DeviceId = row.DeviceId,
                BuildingId = row.BuildingId,
                CompanyId = row.CompanyId,
                ElevatorTypeId = row.ElevatorTypeId,
                IsFunctioning = row.IsFunctioning,
                Device =
                {
                    ["DeviceName"] = row.Name,
                    ["CompanyName"] = row.CompanyName,
                    ["BuildingName"] = row.BuildingName,
                    ["ElevatorType"] = row.ElevatorType,
                    ["MaxFloor"] = row.NumberOfFloors
                },
            }).Single();

            return (true, "Elevator loaded",device);
        }
        catch (Exception e)
        {
            return (false, e.Message,null);
        }
    }

    public async Task<(bool status, string message, Dictionary<string, dynamic> data)> LoadMetadataForElevatorByIdAsync(Guid deviceId)
    {
        try
        {
            using IDbConnection conn = new SqlConnection(_connectionString);
            var queryString =
                "SELECT * from (select Elevator.Id, [key], [value],(select 'device') as 'type' from ElevatorMetaInformation, Elevator WHERE ElevatorMetaInformation.ElevatorId = Elevator.Id";
            queryString += " UNION ";
            queryString +=
                "SELECT elevator.id, [key], [value], (select 'type') AS 'type' FROM ElevatorTypeMetaInformation, Elevator WHERE ElevatorTypeMetaInformation.ElevatorTypeId = elevator.ElevatorTypeId) AS Result WHERE Result.Id = @elevator_id;";

            var query =
                (await conn.QueryAsync(
                    queryString,
                    new {elevator_id = deviceId})
                ).ToList();

            Dictionary<string, dynamic?> remoteMetaDictionary = new();

            {
                foreach (var row in query)
                {
                    if (!remoteMetaDictionary.ContainsKey(row.type))
                    {
                        remoteMetaDictionary[row.type] = new Dictionary<string, dynamic?>();
                    }

                    remoteMetaDictionary[row.type][row.key] = row.value;
                }

                return (true, "Data loaded", remoteMetaDictionary);
            }
        } catch (Exception e) {
                return (false, e.Message, null);
        }
    }

    public async Task<(bool status, string message)> SetFunctionalityInDbById(Guid id, string value)
    {
        try {
            using IDbConnection conn = new SqlConnection(_connectionString);
            var query = "UPDATE Elevator SET IsFunctioning = '" + (bool.Parse(value) ? '1' : '0') +
                        "' WHERE Id = @ElevatorId;SELECT @@ROWCOUNT;";
            var changedRows = await conn.QueryAsync(query, new { ElevatorId = id });
            return  (changedRows.Any(), changedRows.Any()?"Update successful":"Update not successful");
        }catch (Exception e) {
            return (false, e.Message);
        }
    }

    public async Task<bool> UpdateElevator(Guid id, Dictionary<string,dynamic> changedValues) {
        try
        {
            using IDbConnection conn = new SqlConnection(_connectionString);
            var query = "";
            changedValues.ToList().ForEach(item =>
            {
                if (item.Key.Equals("IsFunctioning") || item.Value == null) return;
                query +=
                    $"UPDATE ElevatorMetaInformation " +
                    $"SET [value]='{item.Value}' " +
                    $"WHERE [key]='{item.Key}' " +
                    $"AND ElevatorId = '{id.ToString()}' " +
                    "IF @@ROWCOUNT = 0 " +
                    "INSERT INTO ElevatorMetaInformation " +
                    $"VALUES ('{id.ToString()}','{item.Key}','{item.Value}');";
            });
            if (!string.IsNullOrEmpty(query))
                await conn.QueryAsync(query);
            return true;
        } catch(Exception e) {
            Console.WriteLine(e.Message);
            return false;
        }
    }

    public async Task<List<Guid>> GetListOfElevatorIds()
    {
        var query = "select Id from Elevator";
        using IDbConnection conn = new SqlConnection(_connectionString);
        var result = (await conn.QueryAsync(query)).Select(device => (Guid)device.Id).ToList();
        return await Task.FromResult(result);
    }

    public async Task<bool> UpdateLogWithEvent(List<ElevatorLog> list) {
        var updateQuery = "INSERT INTO ElevatorLog ([ElevatorId], [TimeStamp], [LogDescription], [EventType], [EventResult], [EventPreviousValue]) VALUES";
        foreach(var listItem in list)
        {
            updateQuery += $"('{listItem.ElevatorId}',{listItem.TimeStamp},'{listItem.Description}','{listItem.EventType}','{listItem.NewValue}','{listItem.OldValue}')";
            if (!listItem.Equals(list.Last())) updateQuery += ",";
        } try{
            using IDbConnection conn = new SqlConnection(_connectionString);
            await conn.QueryAsync(updateQuery);
            return true;
        } catch(Exception e){
            Console.WriteLine(e.Message);
            return false;
        }
    }

    public async Task<bool> RemoveListOfMetaData(Guid deviceId, List<string> keys)
    {
        var deleteQuery = $"DELETE FROM ElevatorMetaInformation WHERE [ElevatorId] IN ('{deviceId}') AND [key] IN (";

        foreach(var key in keys)
        {
            deleteQuery += $"'{key}'";
            if(!key.Equals(keys.Last())) deleteQuery += ",";
        }

        deleteQuery += ");";

        try{
            using IDbConnection conn = new SqlConnection(_connectionString);
            await conn.QueryAsync(deleteQuery);
            return true;
        }
        catch(Exception e){
            Console.WriteLine(e.Message);
            return false;
        }
    }

    public async Task<Breakdown>GetCurrentBreakdownIfExists(Guid deviceId)
    {
        using IDbConnection conn = new SqlConnection(_connectionString);
        try
        {
            const string breakdownQuery = "SELECT ElevatorId, Id, CreatedAt from Breakdown " +
                                 "WHERE EXISTS " +
                                 "(SELECT ElevatorId, Id FROM BreakdownTask " +
                                 "WHERE BreakdownTask.BreakdownId = Breakdown.Id " +
                                 "AND BreakdownTask.ElevatorId = ElevatorId " +
                                 "AND RepairDate IS NULL " +
                                 ") AND ElevatorId = @ElevatorId;";

            var breakdownResult = await conn.QuerySingleOrDefaultAsync(breakdownQuery,
                new {ElevatorId = deviceId});

            if (breakdownResult is null)
                return null;

            const string taskQuery = 
                                "SELECT Id, Reason, RepairDate FROM BreakdownTask " +
                                 "WHERE BreakdownId = @breakdownId " +
                                 "AND ElevatorId = @elevatorId " +
                                 "AND RepairDate IS NULL;";

            var taskResult = await conn.QueryAsync(taskQuery, new
                {breakdownId = breakdownResult.Id, elevatorId = breakdownResult.ElevatorId});
            
            var taskList = 
                taskResult
                    .Select(
                        row => new BreakdownTask(row.Id, row.Reason, row.RepairDate)
                    ).ToList();

            var b = new Breakdown(breakdownResult.Id, breakdownResult.ElevatorId, taskList, breakdownResult.CreatedAt);
            Chalk.Red($"Breakdown found, from {breakdownResult.CreatedAt}");
            taskList.ForEach(task =>
            {
                var (status, message) = task.GetStatus();
                if(status)
                    Chalk.Green(message);
                else Chalk.Red(message);
            });
            return b;
        }
        catch (Exception e)
        {
            Chalk.Red(e.Message);
            return null;
        }
    }

    public async Task<bool> RegisterBreakdown(Breakdown breakdown)
    {
        var localBreakdown = breakdown;
        Console.WriteLine("id: "+localBreakdown.GetElevatorId());
        using IDbConnection conn = new SqlConnection(_connectionString);
        try
        {
            Console.WriteLine("id: "+localBreakdown.GetElevatorId());

            var insertGuid = await conn.QueryFirstOrDefaultAsync<Guid>(
                $"INSERT INTO Breakdown ([ElevatorId]) OUTPUT Inserted.Id VALUES (@ElevatorId);", 
                new { ElevatorId = localBreakdown.GetElevatorId()});

            var tasks = await localBreakdown.GetTaskList();
            var taskQuery = "INSERT INTO BreakdownTask (ElevatorId, BreakdownId, Reason) VALUES ";

            foreach (var task in tasks)
            {
                taskQuery += $"('{breakdown.GetElevatorId()}','{insertGuid}', '{task.GetReason()}')";
                if (task != tasks.Last())
                    taskQuery += ", ";
            }

            Console.WriteLine(taskQuery);

            var result = await conn.ExecuteAsync(taskQuery);
            return result == tasks.Count;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
    }

    public async Task<(bool status,string data)> GetConnectionstringForIdAsync(Guid deviceId)
    {
        using IDbConnection conn = new SqlConnection(_connectionString);
        try
        {
            string deviceConnectionstring = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT ConnectionString FROM Elevator WHERE Id = @ElevatorId", new { ElevatorId = deviceId });
            return (true, deviceConnectionstring);
        }
        catch (Exception e)
        {
            return (false, e.Message);

        }
    }

    public async Task<(bool status, string data)> UpdateConnectionStringForElevatorByIdAsync(Guid deviceId)
    {
        using var http = new HttpClient();
        var result = await http.PostAsJsonAsync(ConnectUrl, new { ElevatorId = deviceId });
        var deviceConnectionstring = await result.Content.ReadAsStringAsync();
        using IDbConnection conn = new SqlConnection(_connectionString);
        try
        {
            var queryResult = await conn.ExecuteAsync(
                "UPDATE Elevator SET ConnectionString = @ConnectionString WHERE Id = @ElevatorId",
                new { ElevatorId = deviceId, ConnectionString = deviceConnectionstring }
            );
            return queryResult > 0 ? (true, "Connectionstring updated") : (false, "Connectionstring could not be updated");
        }
        catch (Exception e)
        {
            return (false,e.Message);
        }
    }
}
