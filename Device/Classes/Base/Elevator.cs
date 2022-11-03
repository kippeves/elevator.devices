using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Windows.Markup;
using Dapper;
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
    private IDatabaseService _databaseService = new DatabaseService();
    private protected DeviceClient? DeviceClient;
    private readonly string _connect_Url = "https://kyhelevator.azurewebsites.net/api/Connect";
    private Guid _deviceId;
    private readonly string _connectionString = "Server=tcp:kristiansql.database.windows.net,1433;Initial Catalog=azuresql;Persist Security Info=False;User ID=SqlAdmin;Password=9mFZPHjpgoH3KCKwHbmx;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
    private readonly DeviceInfo _desiredInfo;
    private DeviceInfo _deviceInfo;
    private protected bool Connected = false;

    protected Elevator(DeviceInfo desiredInfo)
    {
        _desiredInfo = desiredInfo;
    }

    public virtual async Task SetupAsync()
    {
        _deviceId = _desiredInfo.DeviceId;
        using IDbConnection conn = new SqlConnection(_connectionString);

        var deviceConnectionstring = "";
        try
        {
            deviceConnectionstring = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT ConnectionString FROM Elevator WHERE Id = @ElevatorId", new { ElevatorId = _deviceId });
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        if (string.IsNullOrEmpty(deviceConnectionstring))
        {
            Console.WriteLine("Initializing connectionstring. Please wait...");
            using var http = new HttpClient();
            var result = await http.PostAsJsonAsync(_connect_Url, new { ElevatorId = _deviceId });
            deviceConnectionstring = await result.Content.ReadAsStringAsync();
            try
            {
                await conn.ExecuteAsync(
                    "UPDATE Elevator SET ConnectionString = @ConnectionString WHERE Id = @ElevatorId",
                    new { ElevatorId = _deviceId, ConnectionString = deviceConnectionstring }
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

            var twinCollection = new TwinCollection();
            _deviceInfo = (await conn.QueryAsync("select * from ElevatorWithInfo where DeviceId = @ElevatorId",
                new { ElevatorId = _deviceId })).Select(row=> new DeviceInfo()
            {
                DeviceId = row.DeviceId,
                BuildingId = row.BuildingId,
                CompanyId = row.CompanyId,
                ElevatorTypeId = row.ElevatorTypeId,
                IsFunctioning = twinCollection["IsFunctioning"] = row.IsFunctioning,
                Device = 
                {
                    ["DeviceName"] = twinCollection["DeviceName"] =  row.Name,
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
            if (query.Any()) {
                foreach (var row in query){
                    if (!remoteMetaDictionary.ContainsKey(row.type)){
                        remoteMetaDictionary[row.type] = new Dictionary<string, string>();
                    }
                    remoteMetaDictionary[row.type][row.key] = row.value;
                }
            };
            
            _deviceInfo.Meta = remoteMetaDictionary;

            await UpdateReportedProperties();
            var twin = await DeviceClient.GetTwinAsync();

            Console.WriteLine($"Elevator loaded: [{twin.Properties.Reported["ElevatorType"]}]\tCompany: [{twin.Properties.Reported["CompanyName"]}]\tBuilding: [{twin.Properties.Reported["BuildingName"]}]");
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
        //deviceInfo.Meta["DoorsAreOpen"]
        var keyName = "DoorsAreOpen";
        using IDbConnection conn = new SqlConnection(_connectionString);

        if(!_deviceInfo.Meta.ContainsKey(keyName))
        {
            try
            {
                var result = await conn.QueryFirstAsync<bool>(
                    "SELECT value FROM ElevatorMetaInformation WHERE ElevatorMetaInformation.ElevatorId = @ElevatorId AND ElevatorMetaInformation.key = @key",
                    new {ElevatorId = _deviceId, key = keyName}
                );
                _deviceInfo.Meta.Add(keyName, result);
            }
            catch
            {
                _deviceInfo.Meta.Add(keyName, false);
            }
        }
        //1. change local state over opened or closed
        _deviceInfo.Meta[keyName] = !_deviceInfo.Meta[keyName];

        //5. return 200 if all is ok, return message on detail that are not ok if they occur
        return new MethodResponse(new byte[0], 200);
    }

    public async Task Loop()
    {
        while (true)
        {
            if (!Connected) continue;
            await UpdateReportedProperties();
            Console.WriteLine($"ID:[{_deviceInfo.DeviceId}]\t{_deviceInfo.Device["DeviceName"].ToString()}: I'm updated my twin!");
            await Task.Delay(_deviceInfo.Device["Interval"]);
        }
    }

    public async Task UpdateReportedProperties()
    {
        TwinCollection newTwin = new TwinCollection()
        {
            ["DeviceName"] = _deviceInfo.Device["DeviceName"],
            ["CompanyName"] = _deviceInfo.Device["CompanyName"],
            ["BuildingName"] = _deviceInfo.Device["BuildingName"],
            ["ElevatorType"] = _deviceInfo.Device["ElevatorType"],
        };
//        _databaseService.UpdateElevatorMetaInfo()
        newTwin["meta"] = JObject.FromObject(_deviceInfo.Meta["device"]);

        await DeviceClient.UpdateReportedPropertiesAsync(newTwin);
    }
}