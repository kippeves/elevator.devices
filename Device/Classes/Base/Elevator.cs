using Device.Interfaces;
using Device.Models;
using Device.Services;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartApp.CLI.Device.Models;
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
        var standardResponse = (int htmlCode, bool success, string? value, string? message) =>
        {
            var twin = new TwinCollection()
            {
                ["Success"] = success,
                ["Value"] = value,
                ["Message"] = message
            };
            Console.WriteLine(twin["Message"]);
            return new MethodResponse(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twin)),
                htmlCode
            );
        };

        RemoveMetaDataRequest request = null!;
        try{
            request = JsonConvert.DeserializeObject<RemoveMetaDataRequest>(methodRequest.DataAsJson)!;
        }
        catch(Exception e){
            return standardResponse(400, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        var oldValues = new TwinCollection();
        
        try
        {
            foreach(var key in request.Keys)
            {
                oldValues[key] = _deviceInfo!.Meta["device"]![key];
            }
        }
        catch(Exception e){
            Console.WriteLine(e.Message);
            return standardResponse(500, false, "Reset Failed", "Exception Happened: Database was not changed");
        }
        

        //1. Databasanrop
        try{
            if(!await _databaseService.RemoveListOfMetaData(_deviceId, request.Keys))
            {
                return standardResponse(500, false, "Reset Failed", "Exception Happened: Database was not changed");
            }
        } catch(Exception e) {
            return standardResponse(500, false, "Reset Failed", "Exception Happened: " + e.Message);
        }
        //2. Twinuppdatering
        //3. self check
        try{
            foreach(var key in request.Keys)
            {
                await ChangeMetaValue(key, null!);
            }
            await UpdateTwin();
        } catch(Exception e) {
            return standardResponse(500, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        var description = "Removed Metadata: ";
        request.Keys.ForEach(key => description += key + " ");
        var OldValuesString = "";
        request.Keys.ForEach( key => OldValuesString += $"{key}: {oldValues[key] }");

        await _logService.AddAsync(description, "Metadata Removal", OldValuesString, "null");
        foreach(var key in request.Keys)
        {
            await _changeService.SetChanged(key);
        }

        return standardResponse(200, true, "Reset Succeded", "Metadata is successfully reset");
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
        return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(newValue, description))), 200);
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


    public async Task<(bool Status, string Message)> ToggleDoors()

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
            return (true, description);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }
    public async Task<MethodResponse> OpenCloseDoor(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"Starting OpenCloseDoor for: {_deviceInfo.Device["DeviceName"]}");
        var toggleDoors = await ToggleDoors();
        var response = await CreateResponse(toggleDoors.Status, toggleDoors.Message);
        return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), toggleDoors.Status?200:500); 
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

    public async Task<MethodResponse> MoveToFloor(MethodRequest methodRequest, object userContext) {
        var keyName = "CurrentFloor";
        if (!_deviceInfo!.IsFunctioning)
        {

            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false, "Method cannot be accessed while the elevator is offline."))), 500);
        }

        FloorChangeRequest request;
        try
        {

            request = JsonConvert.DeserializeObject<FloorChangeRequest>(methodRequest.DataAsJson);
        }
        catch (Exception e)
        {
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false,"You did not provide any information with the call"))), 500);
        }
        
        int currentFloor = int.Parse(_deviceInfo.Meta["device"][keyName]);
        int newFloor = request.FloorNumber.Value;

        var errors = new List<string>();

        if (await AreDoorsOpen())
        {
            var result = await ToggleDoors();
            if (!result.Status)
                errors.Add(result.Message);
            await Task.Delay(500);
        }

        if (!request.FloorNumber.HasValue)
            errors.Add("You never picked a floor to move to.");
        if (request.FloorNumber.HasValue && request.FloorNumber.Equals(currentFloor))
            errors.Add("You can't go to the floor you're on. Please pick a different floor.");
        if (!request.WeightAmount.HasValue)
            errors.Add("Are you weightless? You need to have a weight to ride this elevator!");
        if(request.FloorNumber <= 0 || request.FloorNumber > _deviceInfo.Device["MaxFloor"] )
            errors.Add("You cannot go to that floor.");

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
                return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false,returnString))), 500);
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
                        Console.WriteLine($"{_deviceInfo.DeviceId} is currently on floor {i}.");
                    }

                    break;
                case 1:
                    for (var i = currentFloor; i >= newFloor; i--)
                    {
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
            var x = $"{_deviceInfo.DeviceId} stopped at Floor {newFloor}";
            var result = await CreateResponse(false, x);
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)), 200);
        }
        catch (Exception e)
        {
            var response = await CreateResponse(false, e.Message);
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), 500);
        }
    }

    private async Task<TwinCollection> CreateResponse(bool status, string description) {
        var twin = new TwinCollection()
        {
            ["Value"] = status,
            ["Message"] = description
        };
        return twin;
    }
}