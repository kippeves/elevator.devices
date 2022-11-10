using Device.Interfaces;
using Device.Models;
using Device.Services;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text;

namespace Device.Classes.Base;

class Elevator
{
    protected readonly Guid _deviceId;
    
    protected DeviceInfo? _deviceInfo;
    protected DeviceClient _deviceClient;

    protected bool _connected = false;
    
    private ILogService? _logService;
    private IChangeService? _changeService;
    private readonly IDatabaseService _databaseService;

    public Elevator(string id, IDatabaseService databaseService){
        _deviceId = new Guid(id);
        _databaseService = databaseService;
    }

    public virtual async Task SetupAsync()
    {
        var deviceConnectionstring = "";

        var (status, data) = await _databaseService.GetConnectionstringForIdAsync(_deviceId);
        if (status)
        {
            deviceConnectionstring = data;
        }
        else Console.Write(data);

        if (string.IsNullOrEmpty(deviceConnectionstring))
        {
            Console.WriteLine("Initializing connectionstring. Please wait...");
            var updateResult = await _databaseService.UpdateConnectionStringForElevatorByIdAsync(_deviceId);
            deviceConnectionstring = updateResult.data;
        }

        try
        {
            _deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionstring);
            var deviceResult = await _databaseService.GetElevatorByIdAsync(_deviceId);
            if (deviceResult.status)
            {
                _deviceInfo = deviceResult.data;
            }
            else Console.Write(deviceResult.message);

            var metaQuery = await _databaseService.LoadMetadataForElevatorByIdAsync(_deviceId);
            
            if (metaQuery.status)
                _deviceInfo!.Meta = metaQuery.data!;
            
            var keys= 
                ((Dictionary<string, dynamic>)_deviceInfo!.Meta["device"]!)
                .Select(row => row.Key).ToList();

            // Get List Of Keys in metadata-list
            _changeService = new ChangeService(keys);
            _logService = new LogService(_deviceId, _databaseService);

            var twin = await _deviceClient.GetTwinAsync();
            Console.WriteLine($"Elevator loaded: [{twin.Properties.Reported["ElevatorType"]}]\tCompany: [{twin.Properties.Reported["CompanyName"]}]\tBuilding: [{twin.Properties.Reported["BuildingName"]}]");
            await _deviceClient.SetMethodHandlerAsync("ToggleFunctionality", ToggleFunctionality, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("OpenCloseDoor", OpenCloseDoor, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("MoveToFloor", MoveToFloor, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("RemoveMetaData", RemoveMetaData, _deviceClient);
            _connected = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            _connected = false;
        }
    }

    public async Task<MethodResponse> RemoveMetaData(MethodRequest methodRequest, object userContext)
    {
        try{
            Console.WriteLine($"Starting ResetElevator for: {_deviceInfo.Device["DeviceName"]}");
            var message = "Metadata for Elevator was reset";
            return new(
                Encoding.UTF8.GetBytes(message),
                200
            );
        }
        catch(Exception e){
            Console.WriteLine("Exception Happened: " + e.Message);
            return new MethodResponse(
                Encoding.UTF8.GetBytes(e.Message),
                500
            );
        }
    }
    public async Task ChangeMetaValue(string key, string value)
    {
        if (_deviceInfo.Meta["device"].ContainsKey(key))
        {
            _deviceInfo.Meta["device"][key] = value;
        }
        else
        {
            _deviceInfo.Meta["device"][key] = null;
        }
    }

    public async Task<MethodResponse> ToggleFunctionality(MethodRequest methodrequest, object usercontext)
    {
        Console.WriteLine($"Toggling functionality for elevator {_deviceInfo.DeviceId}");
        var oldValue = _deviceInfo.IsFunctioning;
        var newValue = !oldValue;
        _deviceInfo.IsFunctioning = newValue;

        var description = newValue ? "Elevator is currently online." : "Elevator is currently offline.";
        var eventType = newValue ? "Elevator_Online" : "Elevator_Offline";

        await _logService.AddAsync(description, eventType, oldValue ? "Online" : "Offline", newValue ? "Online" : "Offline");

        var (status, message) = await _databaseService.SetFunctionalityInDbById(_deviceInfo.DeviceId, newValue.ToString());
            if (!status) return new MethodResponse(Encoding.UTF8.GetBytes(message), 500);

        await _deviceClient.UpdateReportedPropertiesAsync(new TwinCollection() { ["IsFunctioning"] = _deviceInfo.IsFunctioning });
        await _changeService.SetChanged("IsFunctioning");
        Console.WriteLine($"{_deviceInfo.DeviceId}\t{description}");
        var twin = new TwinCollection()
        {
            ["Value"] = newValue,
            ["Message"] = description
    };
        return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twin)), 200);
    }


    public async Task<bool> AreDoorsOpen() {
        string key = "DoorsAreOpen";
        if (_deviceInfo.Meta["device"].ContainsKey(key))
        {
            bool status = _deviceInfo.Meta["device"][key].Equals("1") ? true : false;
            return status;
        }
        else await ChangeMetaValue(key, "false");
        return false;
    }


    public async Task<(bool Status, string Message)?> ToggleDoors()
    {
        if (!_deviceInfo.IsFunctioning)
        {
            String message = "Method cannot be accessed while the elevator is offline.";
            Console.WriteLine(message);
            return (false, message);
        }
        var keyName = "DoorsAreOpen";
        Console.WriteLine("Value: " + _deviceInfo.Meta["device"][keyName]);
        try
        {
            bool oldValue = _deviceInfo.Meta["device"][keyName].Equals("1");
            bool newValue = !oldValue;
            await ChangeMetaValue(keyName, (newValue ? "1" : "0"));
            var description = newValue ? "Elevator Doors Are Open" : "Elevator Doors Are Closed";
            var eventType = newValue ? "Doors_Open" : "Doors_Close"; // Doors_WalkedAway
            await _logService.AddAsync(description, eventType, oldValue ? "True" : "False", newValue ? "True" : "False");
            Console.WriteLine($"{_deviceInfo.DeviceId}\t{description}");
            await _changeService.SetChanged(keyName);
            return (newValue, "Action successful");
        }
        catch (Exception e)
        {
            return null;
        }
    }
    public async Task<MethodResponse> OpenCloseDoor(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"Starting OpenCloseDoor for: {_deviceInfo.Device["DeviceName"]}");
        var toggleDoors = await ToggleDoors();
        
        var twin = new TwinCollection()
        {
            ["Value"] = toggleDoors.Value.Status,
            ["Message"] = toggleDoors.Value.Message
        };
        //return toggleDoors.Value.Status ?
        //new(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twin)), 200):
        //new (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twin)), 500);
        return new(
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twin)),
            toggleDoors.HasValue ? 200 : 500
            );
    }

    public async Task Loop()
    {
        while (true)
        {
            if (!_connected){
                var source = new CancellationTokenSource();
                source.Cancel();
            }

            if (await _changeService.HasAnyChangesBeenDone())
            {
                List<Task> tasks = new()
                {
                    UpdateTwin()
                };

                if (_logService!.GetList().Any())
                    tasks.Add(_logService.PushToDatabaseAsync());

                // Kollar vilka värden som är ändrade från tidigare. 

                var changedValues = await _changeService.GetChanged();
                
                var valuesThatHaveChangedSinceLastRun = ((Dictionary<string, dynamic>) _deviceInfo!.Meta["device"]!)
                    .ToList()
                    .Where(item => changedValues.Contains(item.Key))
                    .ToDictionary(item => item.Key, item => item.Value);

                await _databaseService.UpdateElevator(_deviceInfo.DeviceId, valuesThatHaveChangedSinceLastRun);
                Task.WaitAll(tasks.ToArray());
                await _changeService.ClearChanges();
            }
            await Task.Delay(_deviceInfo.Device["Interval"]);
        }
    }

    public async Task UpdateTwin()
    {
        TwinCollection newTwin = new()
        {
            ["IsFunctioning"] = _deviceInfo.IsFunctioning,
            ["DeviceName"] = _deviceInfo.Device["DeviceName"],
            ["CompanyName"] = _deviceInfo.Device["CompanyName"],
            ["BuildingName"] = _deviceInfo.Device["BuildingName"],
            ["ElevatorType"] = _deviceInfo.Device["ElevatorType"],
            ["meta"] = JObject.FromObject(_deviceInfo.Meta["device"])
        };

        Console.WriteLine($"ID:[{_deviceInfo.DeviceId}]\t{_deviceInfo.Device["DeviceName"].ToString()}: I've updated my twin!");
        await _deviceClient.UpdateReportedPropertiesAsync(newTwin);
    }

    public async Task<MethodResponse> MoveToFloor(MethodRequest methodRequest, object userContext)
    {
        var keyName = "CurrentFloor";
        if (!_deviceInfo!.IsFunctioning)
        {
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject("Method cannot be accessed while the elevator is offline.")), 500);
        }

        FloorChangeRequest request;
        try
        {

            request = JsonConvert.DeserializeObject<FloorChangeRequest>(methodRequest.DataAsJson);
        }
        catch (Exception e)
        {
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject("You did not provide any information with the call")), 500);
        }
        
        int currentFloor = int.Parse(_deviceInfo.Meta["device"][keyName]);
        int newFloor = request.FloorNumber.Value;

        var errors = new List<string>();

        if (await AreDoorsOpen())
        {
            var result = await ToggleDoors();
            if (!result.Value.Status)
                errors.Add(result.Value.Message);
            await Task.Delay(500);
        }

        if (!request.FloorNumber.HasValue)
            errors.Add("You never picked a floor to move to.");
        if (request.FloorNumber.HasValue && request.FloorNumber.Equals(currentFloor))
            errors.Add("You can't go to the floor you're on. Please pick a different floor.");
        if (!request.WeightAmount.HasValue)
            errors.Add("Are you weightless? You need to have a weight to ride this elevator!");

        try
        {
            ((Dictionary<string, dynamic>)_deviceInfo!.Meta["type"]!).TryGetValue("MaxWeight", out var typeMaxWeight);
            ((Dictionary<string, dynamic>)_deviceInfo!.Meta["device"]!).TryGetValue("MaxWeight",
                out var deviceMaxWeight);

            if (deviceMaxWeight != null || typeMaxWeight != null)
            {
                int compareWeight = int.Parse(deviceMaxWeight ?? typeMaxWeight);
                //Enhetens maxvikt har företräde före typens maxvikt. Om båda är satta så är det enhetens värde som gäller.
                if (request.WeightAmount > compareWeight)
                    errors.Add("You exceed the maximum weightlimit set for this elevator. You currently cannot ride this elevator. Please remove some weight.");
            }
            if (errors.Any())
            {
                var returnString = "";
                errors.ForEach(error => returnString += error);
                return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(returnString)), 500);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        Console.WriteLine($"Changing floor for {_deviceInfo.Device["DeviceName"]}");
        Console.WriteLine($"Current Floor: {currentFloor}");
        Console.WriteLine($"Floor asked to move to : {newFloor}");
        try
        {
            var description = $"Elevator moved from floor {currentFloor} to {newFloor}";
            Console.WriteLine(
                $"{_deviceInfo.DeviceId} started moving from floor {currentFloor.ToString()} to {newFloor.ToString()}");
            
            await _logService!.AddAsync(description, "Elevator Started", currentFloor.ToString(),
                    newFloor.ToString());

            int relation = Math.Sign(currentFloor.CompareTo(newFloor));
            switch (relation)
            {
                case -1:
                    for (var i = currentFloor; i <= newFloor; i++)
                    {
                        await Task.Delay(1000);
                        Console.WriteLine($"{_deviceInfo.DeviceId} is currently on floor {i}.");
                    }

                    break;
                case 1:
                    for (var i = currentFloor; i >= newFloor; i--)
                    {
                        await Task.Delay(1000);
                        Console.WriteLine($"{_deviceInfo.DeviceId} is currently on floor {i}.");
                    }

                    break;
            }

            await Task.Delay(500).ContinueWith(_ => _logService!.AddAsync(description, "Elevator Arrived",
                currentFloor.ToString(),
                newFloor.ToString()));

            Console.WriteLine($"{_deviceInfo.DeviceId} stopped at Floor {newFloor}");
            await Task.Delay(500).ContinueWith(task => ToggleDoors());
            await ChangeMetaValue(keyName, newFloor.ToString());
            await _changeService!.SetChanged(keyName);
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject($"{_deviceInfo.DeviceId} stopped at Floor {newFloor}")), 200);
        }
        catch (Exception e)
        {
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(e.Message)), 500);
        }
    }

}