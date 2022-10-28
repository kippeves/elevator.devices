using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using SmartApp.CLI.Device.Models;

namespace SmartApp.CLI.Device.Classes.Base
{
    abstract class IoTDevice
    {
        private protected DeviceClient _deviceClient;
        private readonly string _connect_Url = "https://kristiankyh.azurewebsites.net/api/devices/connect?";
        private string _deviceId = "";
        private readonly string _connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\kippe\\source\\repos\\Lektion11\\Data\\db.mdf;Integrated Security=True;Connect Timeout=30";
        protected DeviceInfo deviceInfo;
        private readonly DeviceInfo? _desiredInfo;
        private protected bool Connected = false;

        public IoTDevice(DeviceInfo? desiredInfo)
        {
            _desiredInfo = desiredInfo;
        }

        public virtual async Task SetupAsync()
        {
            if (_desiredInfo == null) _deviceId =  "";
            else
            {
                //using var hmacMd5 = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_desiredInfo.Location));
                //var buffer = System.Text.Encoding.UTF8.GetBytes(_desiredInfo.DeviceName);
                //_deviceId = Convert.ToHexString(hmacMd5.ComputeHash(buffer));
            }

            var searchedId = "";
            using IDbConnection conn = new SqlConnection(_connectionString);
            try
            {
                searchedId = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT DeviceId FROM DeviceInfo where DeviceId = @deviceId", new {deviceId = _deviceId});
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            if (string.IsNullOrEmpty(searchedId)){
                try
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO DeviceInfo (DeviceId, DeviceName, DeviceType, Location, Owner, Interval) VALUES (@DeviceId, @DeviceName, @DeviceType, @Location, @Owner, @Interval)",
                        new
                        {
                            //DeviceId = _deviceId, DeviceName = _desiredInfo?.DeviceName ?? "Generic Device",
                            //DeviceType = _desiredInfo?.DeviceType ?? "default",
                            //Location = _desiredInfo?.Location ?? "none", 
                            //Owner = _desiredInfo?.Owner ?? "default",
                            //Interval = _desiredInfo?.Interval ?? 60000
                        });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            var deviceConnectionstring = "";
            try
            {
                deviceConnectionstring = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT ConnectionString FROM DeviceInfo WHERE DeviceId = @DeviceId", new {DeviceId = _deviceId});
            }
            catch (Exception e)
            {
                //Console.WriteLine($"1:{deviceInfo.DeviceType}/{deviceInfo.DeviceName}/{deviceInfo.Location} Could not connect. {e.Message}");
            }

            if (string.IsNullOrEmpty(deviceConnectionstring))
            {
                Console.WriteLine("Initializing connectionstring. Please wait...");
                using var http = new HttpClient();
                var result = await http.PostAsJsonAsync(_connect_Url + $"deviceId={_deviceId}", new { deviceId = _deviceId });
                deviceConnectionstring = await result.Content.ReadAsStringAsync();
                try {
                    await conn.ExecuteAsync(
                        "UPDATE DeviceInfo SET ConnectionString = @ConnectionString WHERE DeviceId = @DeviceId",
                        new {DeviceId = _deviceId, ConnectionString = deviceConnectionstring}
                    );
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"{deviceInfo.DeviceType}/{deviceInfo.DeviceName}/{deviceInfo.Location} Could not connect. {e.Message}");
                }
            }

            try
            {
                _deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionstring);
                deviceInfo = await conn.QueryFirstOrDefaultAsync<DeviceInfo>("select * from DeviceInfo where DeviceId = @DeviceId",
                    new { DeviceId = _deviceId });

                var twinCollection = new TwinCollection
                {
                    //["deviceName"] = deviceInfo.DeviceName,
                    //["deviceType"] = deviceInfo.DeviceType,
                    //["location"] = deviceInfo.Location,
                    //["owner"] = deviceInfo.Owner,
                    //["interval"] = deviceInfo.Interval
                };

                var twin = await _deviceClient.GetTwinAsync();
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
                await _deviceClient.SetMethodHandlerAsync("EditInterval", EditInterval, _deviceClient);

                Console.WriteLine($"[{twin.Properties.Reported["deviceType"]}]{twin.Properties.Reported["deviceName"]} created in room {twin.Properties.Reported["location"]}");
                Connected = true;
            }
            catch (Exception e)
            {
                //Console.WriteLine($"3:{deviceInfo.DeviceType}/{deviceInfo.DeviceName}/{deviceInfo.Location} Could not connect. {e.Message}");
                Connected = false;
            }
        }

        private Task<MethodResponse> EditInterval(MethodRequest methodrequest, object usercontext)
        {
            var json = JsonConvert.DeserializeObject<RequestPayload>(methodrequest.DataAsJson);
            //Console.WriteLine(JsonConvert.SerializeObject(json));
            //if (!json.Interval.Equals(0))
            //{
            //    if(json.Interval >= 20000){
            //        _deviceClient.UpdateReportedPropertiesAsync(new TwinCollection(){["interval"] = json.Interval}).ConfigureAwait(false);
            //        Console.WriteLine("Interval is now "+ json.Interval);
            //    } else Console.WriteLine("No");
            //}
            //else return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"Interval must be longer than "+(json.Interval/1000)+" seconds\"}"), 500));
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"Interval is now "+ json.Interval+" ms\"}"), 200));
        }


        public async Task Loop()
        {
            while(true)
            {
                if (!Connected) continue;
                await UpdateReportedProperties();
//                await Task.Delay(deviceInfo.Interval);
            }
        }

        protected virtual async Task<string> HeartbeatInfo()
        {
            //var twin = await _deviceClient.GetTwinAsync();
            var outString = "";
            //    $"Heartbeat sent:\t[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}][Type: {twin.Properties.Reported["deviceType"]}][Room: {twin.Properties.Reported["location"]}][Name: {twin.Properties.Reported["deviceName"]}]";
            return outString;
        }

        protected abstract Task UpdateReportedProperties();
    }
}
