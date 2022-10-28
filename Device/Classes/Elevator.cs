using System.Globalization;
using System.Text;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using SmartApp.CLI.Device.Classes.Base;
using SmartApp.CLI.Device.Models;
using SmartApp.CLI.Device.Models.Requests;

namespace SmartApp.CLI.Device.Classes;

internal class Elevator : IoTDevice
{
    public Elevator(DeviceInfo info) : base(info) { }

    public override async Task SetupAsync()
    {
        await base.SetupAsync();
        //await _deviceClient.SetMethodHandlerAsync("SetSpeed", SetSpeedAsync, null);
        //await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesChanged, _deviceClient);
    }

    //private Task<MethodResponse> SetSpeedAsync(MethodRequest methodrequest, object usercontext)
    //{
    //    var json = JsonConvert.DeserializeObject<FanspeedRequest>(methodrequest.DataAsJson);
    //    Console.WriteLine(JsonConvert.SerializeObject(json));
    //    if (!json.rpm.Equals(0))
    //    {
    //        if(json.rpm >= 3000){
    //            _deviceClient.UpdateReportedPropertiesAsync(new TwinCollection(){["fanspeed"] = json.rpm}).ConfigureAwait(false);
    //            Console.WriteLine("Fanspeed is now "+ json.rpm);
    //        } else Console.WriteLine("No");
    //    }
    //    else return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"Interval must be longer than "+(json.rpm)+" seconds\"}"), 500));
    //    return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"Interval is now "+ json.rpm+" ms\"}"), 200));
    //}

    private async Task OnDesiredPropertiesChanged(TwinCollection desiredProperties, object usercontext)
    {
        //var device = usercontext as DeviceClient;
        //var newReported = new TwinCollection();
        //newReported["rpm"] = null;
        //if (desiredProperties.Contains("rpm"))
        //{
        //    int RequestedRPM = desiredProperties["rpm"];
        //    if (RequestedRPM is >= 0 and <= 1500)
        //    {
        //        _fanSpeedRpm = RequestedRPM;
        //        newReported["rpm"] = _fanSpeedRpm;
        //        switch (_fanSpeedRpm)
        //        {
        //            case > 0:
        //            {
        //                Console.WriteLine($"Fanspeed set to {_fanSpeedRpm} RPM");
        //                if (_fanEnabled == false)
        //                {
        //                    _fanEnabled = true;
        //                    newReported["state"] = true;
        //                }

        //                break;
        //            }

        //            case 0:
        //                _fanEnabled = false;
        //                newReported["state"] = false;
        //                break;
        //        }
        //    }
        //}
        //if (newReported.Count > 0) await device.UpdateReportedPropertiesAsync(newReported);
    }

    protected override async Task UpdateReportedProperties()
    {
        //Console.WriteLine(await HeartbeatInfo());
        //var reportedProperties = new TwinCollection();
        //    reportedProperties["rpm"] = _fanSpeedRpm;
        //    reportedProperties["state"] = _fanEnabled;
        //try
        //{
        //    await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        //}
        //catch (Exception e)
        //{
        //    Console.WriteLine(e.Message);
        //}
    }
}