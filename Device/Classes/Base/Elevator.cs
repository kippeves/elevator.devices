using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Windows.Markup;
using Dapper;
using Device.Interfaces;
using Device.Models;
using Device.Services;
using DotNetty.Transport.Channels;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Device.Classes.Base;

abstract class Elevator
{
    private protected DeviceClient? DeviceClient;
    private readonly string _connect_Url = "https://kyhelevator.azurewebsites.net/api/Connect";
    private Guid _deviceId;

    private readonly string _connectionString =
        "Server=tcp:kristiansql.database.windows.net,1433;Initial Catalog=azuresql;Persist Security Info=False;User ID=SqlAdmin;Password=9mFZPHjpgoH3KCKwHbmx;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

    private readonly DeviceInfo _desiredInfo;
    private DeviceInfo _deviceInfo;
    private protected bool Connected = false;
    private ILogService _logService;
    private IDatabaseService _databaseService;

    protected Elevator(DeviceInfo desiredInfo)
    {
        _desiredInfo = desiredInfo;
        _databaseService = new DatabaseService(_connectionString);
    }

    public virtual async Task SetupAsync()
    {
        _deviceId = _desiredInfo.DeviceId;
        using IDbConnection conn = new SqlConnection(_connectionString);

        var deviceConnectionstring = "";
        try
        {
            deviceConnectionstring = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT ConnectionString FROM Elevator WHERE Id = @ElevatorId", new {ElevatorId = _deviceId});
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        if (string.IsNullOrEmpty(deviceConnectionstring))
        {
            Console.WriteLine("Initializing connectionstring. Please wait...");
            using var http = new HttpClient();
            var result = await http.PostAsJsonAsync(_connect_Url, new {ElevatorId = _deviceId});
            deviceConnectionstring = await result.Content.ReadAsStringAsync();
            try
            {
                await conn.ExecuteAsync(
                    "UPDATE Elevator SET ConnectionString = @ConnectionString WHERE Id = @ElevatorId",
                    new {ElevatorId = _deviceId, ConnectionString = deviceConnectionstring}
                );
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        try
        {
            DeviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionstring);

            await DeviceClient.SetMethodHandlerAsync("OpenCloseDoor", OpenCloseDoor, null);

            var twinCollection = new TwinCollection();
            _deviceInfo = (await conn.QueryAsync("select * from ElevatorWithInfo where DeviceId = @ElevatorId",
                new {ElevatorId = _deviceId})).Select(row => new DeviceInfo()
            {
                DeviceId = row.DeviceId,
                BuildingId = row.BuildingId,
                CompanyId = row.CompanyId,
                ElevatorTypeId = row.ElevatorTypeId,
                IsFunctioning = twinCollection["IsFunctioning"] = row.IsFunctioning,
                Device =
                {
                    ["DeviceName"] = twinCollection["DeviceName"] = row.Name,
                    ["CompanyName"] = twinCollection["CompanyName"] = row.CompanyName,
                    ["BuildingName"] = twinCollection["BuildingName"] = row.BuildingName,
                    ["ElevatorType"] = twinCollection["ElevatorType"] = row.ElevatorType,
                },
            }).Single();

            var query =
                (await conn.QueryAsync(
                    "SELECT * from (select Elevator.Id, [key], [value],(select 'device') as 'type' from ElevatorMetaInformation, Elevator WHERE ElevatorMetaInformation.ElevatorId = Elevator.Id UNION SELECT elevator.id, [key], [value], (select 'type') AS 'type' FROM ElevatorTypeMetaInformation, Elevator WHERE ElevatorTypeMetaInformation.ElevatorTypeId = elevator.ElevatorTypeId) AS Result WHERE Result.Id = @elevator_id;",
                    new {elevator_id = _deviceId})
                ).ToList();

            Dictionary<string, dynamic> remoteMetaDictionary = new();
            if (query.Any())
            {
                foreach (var row in query)
                {
                    if (!remoteMetaDictionary.ContainsKey(row.type))
                    {
                        remoteMetaDictionary[row.type] = new Dictionary<string, dynamic>();
                    }

                    remoteMetaDictionary[row.type][row.key] = row.value;
                }
            }

            ;

            _deviceInfo.Meta = remoteMetaDictionary;
            _deviceInfo.Meta["device"]["DoorsAreOpen"] = false;

            var twin = await DeviceClient.GetTwinAsync();
            _logService = new LogService(_deviceId, _databaseService);
            //await UpdateReportedProperties();
            await UpdateTwin();
            Console.WriteLine(
                $"Elevator loaded: [{twin.Properties.Reported["ElevatorType"]}]\tCompany: [{twin.Properties.Reported["CompanyName"]}]\tBuilding: [{twin.Properties.Reported["BuildingName"]}]");
            Connected = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Connected = false;
        }
    }

    public async Task<MethodResponse> OpenCloseDoor(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"Starting OpenCloseDoor for: {_deviceInfo.Device["DeviceName"]}");
        //deviceInfo.Meta["DoorsAreOpen"]
        var keyName = "DoorsAreOpen";
        using IDbConnection conn = new SqlConnection(_connectionString);
        var oldValue = false;
        var newValue = false;
        try
        {
            Console.WriteLine(_deviceInfo.Meta["device"][keyName]);
            oldValue = _deviceInfo.Meta["device"][keyName];
            newValue = !oldValue;
            _deviceInfo.Meta["device"][keyName] = newValue;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        var description = newValue ? "Elevator Doors Are Open" : "Elevator Doors Are Closed";
        var eventType = newValue ? "Doors_Open" : "Doors_Close"; // Doors_WalkedAway
        await _logService.AddAsync(description, eventType, oldValue ? "True" : "False", newValue ? "True" : "False");
        Console.WriteLine($"OpenCloseDoor Completed for: {_deviceInfo.Device["DeviceName"]}");
        return new MethodResponse(new byte[0], 200);
    }

    public async Task Loop()
    {
        while (true)
        {
            if (!Connected) continue;

            List<Task> tasks = new();
            tasks.Add(UpdateTwin());
            
            if (_logService.GetList().Any())
                tasks.Add(_logService.PushToDatabaseAsync());

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine(
                $"ID:[{_deviceInfo.DeviceId}]\t{_deviceInfo.Device["DeviceName"].ToString()}: I've updated my twin!");

            await Task.Delay(_deviceInfo.Device["Interval"]);
        }
    }

    public async Task UpdateTwin()
    {
        TwinCollection newTwin = new TwinCollection()
        {
            ["DeviceName"] = _deviceInfo.Device["DeviceName"],
            ["CompanyName"] = _deviceInfo.Device["CompanyName"],
            ["BuildingName"] = _deviceInfo.Device["BuildingName"],
            ["ElevatorType"] = _deviceInfo.Device["ElevatorType"],
        };
        newTwin["meta"] = JObject.FromObject(_deviceInfo.Meta["device"]);
        await DeviceClient.UpdateReportedPropertiesAsync(newTwin);
    }
}
