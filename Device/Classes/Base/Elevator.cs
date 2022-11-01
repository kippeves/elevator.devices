using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Runtime.Serialization.Formatters;
using System.Text;
using Dapper;
using Device.Models;
using DotNetty.Transport.Channels;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using SmartApp.CLI.Device.Models;

namespace Device.Classes.Base;

abstract class Elevator
{
    private protected DeviceClient? DeviceClient;
    private readonly string _connect_Url = "https://kyhelevator.azurewebsites.net/api/Connect";
    private Guid _deviceId;
    private readonly string _connectionString = "Server=tcp:kristiansql.database.windows.net,1433;Initial Catalog=azuresql;Persist Security Info=False;User ID=SqlAdmin;Password=9mFZPHjpgoH3KCKwHbmx;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
    private readonly DeviceInfo _desiredInfo;
    private DeviceInfo deviceInfo;
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
            deviceInfo = (await conn.QueryAsync("select * from ElevatorWithInfo where DeviceId = @ElevatorId",
                new { ElevatorId = _deviceId })).Select(row=> new DeviceInfo()
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
                },
            }).Single();



            
            var remoteMetaDictionary=
                (await conn.QueryAsync(
                    "select [key], [value] from ElevatorMetaInformation, Elevator Where ElevatorMetaInformation.ElevatorId = Elevator.Id AND Elevator.Id = @elevator_id",
                    new {elevator_id = _deviceId})
                ).ToDictionary(
                    row => (string)row.key, 
                    row => (dynamic)row.value
                );

            deviceInfo.Meta = remoteMetaDictionary;

            var twinCollection = new TwinCollection
            {
                ["DeviceName"] = deviceInfo.Device["DeviceName"],
                ["CompanyName"] = deviceInfo.Device["CompanyName"],
                ["BuildingName"] = deviceInfo.Device["BuildingName"],
                ["ElevatorType"] = deviceInfo.Device["ElevatorType"],
                ["IsFunctioning"] = deviceInfo.IsFunctioning
            };
            if (deviceInfo.Meta.Count() < 0) twinCollection["meta"] = deviceInfo.Meta;

            await DeviceClient.UpdateReportedPropertiesAsync(twinCollection);
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
    
    public async Task Loop()
    {
        while (true)
        {
            if (!Connected) continue;
            await UpdateReportedProperties();
            await Task.Delay(deviceInfo.Device["Interval"]);
        }
    }

    protected abstract Task UpdateReportedProperties();
}