using System.Text;
using Device.Interfaces;
using Device.Models;
using Device.Services;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Device.Classes.Base;

internal class Elevator
{
    private readonly IDatabaseService _databaseService;
    protected readonly Guid DeviceId;
    private IChangeService _changeService;
    private DeviceClient _deviceClient;

    private DeviceInfo _deviceInfo;

    private ILogService _logService;
    private IRepairService _repairService;

    protected bool Connected;

    public Elevator(Guid id, IDatabaseService databaseService)
    {
        DeviceId = id;
        _databaseService = databaseService;
    }

    public virtual async Task SetupAsync()
    {
        var deviceConnectionstring = "";

        var (status, data) = await _databaseService.GetConnectionstringForIdAsync(DeviceId);
        if (status)
            deviceConnectionstring = data;
        else Console.WriteLine(data);

        if (string.IsNullOrEmpty(deviceConnectionstring))
        {
            Console.WriteLine("Initializing connectionstring. Please wait...");
            var updateResult = await _databaseService.UpdateConnectionStringForElevatorByIdAsync(DeviceId);
            deviceConnectionstring = updateResult.data;
        }

        try
        {
            _deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionstring);
            var deviceResult = await _databaseService.GetElevatorByIdAsync(DeviceId);
            if (deviceResult.status)
                _deviceInfo = deviceResult.data;
            else Chalk.Red(deviceResult.message);

            var metaQuery = await _databaseService.LoadMetadataForElevatorByIdAsync(DeviceId);

            if (metaQuery.status)
                _deviceInfo!.Meta = metaQuery.data!;

            var keys =
                ((Dictionary<string, dynamic>) _deviceInfo!.Meta["device"]!)
                .Select(row => row.Key).ToList();

            // Get List Of Keys in metadata-list
            _changeService = new ChangeService(keys);
            _logService = new LogService(DeviceId, _databaseService);
            _repairService = new RepairService(_logService,_deviceInfo.DeviceId);
            

            var twin = await _deviceClient.GetTwinAsync();
            Chalk.Green(
                $"Elevator loaded: [{twin.Properties.Reported["ElevatorType"]}]\tCompany: [{twin.Properties.Reported["CompanyName"]}]\tBuilding: [{twin.Properties.Reported["BuildingName"]}]");

            var breakdown = await _databaseService.GetCurrentBreakdownIfExists(_deviceInfo.DeviceId);
                _repairService.PreloadFromDatabaseEntry(breakdown);

            
            await _deviceClient.SetMethodHandlerAsync("ToggleFunctionality", ToggleFunctionality, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("OpenCloseDoor", OpenCloseDoor, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("MoveToFloor", MoveToFloor, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("RemoveMetaData", RemoveMetaData, _deviceClient);
            await _deviceClient.SetMethodHandlerAsync("UpdateMetaData", UpdateMetaData, _deviceClient);
            Connected = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Connected = false;
        }
    }

    private async Task ChangeMetaValue(string key, string value)
    {
        await Task.FromResult(_deviceInfo.Meta["device"][key] = value);
    }

    private static bool WillAnAccidentHappen()
    {
        var r = new Random();
        return r.Next(100)>90;
    }

    public async Task<bool> AreDoorsOpen()
    {
        var key = "DoorsAreOpen";
        if (_deviceInfo.Meta["device"].ContainsKey(key))
        {
            var status = _deviceInfo.Meta["device"][key].Equals("1") ? true : false;
            return status;
        }

        await ChangeMetaValue(key, "false");

        return false;
    }

    public async Task<(bool Status, string Message)> ToggleDoors()
    {
        if (!_deviceInfo.IsFunctioning) {
                var message = "Method cannot be accessed while the elevator is offline.";
                Console.WriteLine(message);
                return (false, message);
        }

        const string keyName = "DoorsAreOpen";
        Console.WriteLine("Value: " + _deviceInfo.Meta["device"][keyName]);
        try
        {
            bool oldValue = _deviceInfo.Meta["device"][keyName].Equals("1");
            var newValue = !oldValue;
            await ChangeMetaValue(keyName, newValue ? "1" : "0");
            var description = newValue ? "Elevator Doors Are Open" : "Elevator Doors Are Closed";
            var eventType = newValue ? "Doors_Open" : "Doors_Close"; // Doors_WalkedAway
            await _logService.AddAsync(description, eventType, oldValue ? "True" : "False",
                newValue ? "True" : "False");
            Console.WriteLine($"{_deviceInfo.DeviceId}\t{description}");
            await _changeService.SetChanged(keyName);
            return (true, description);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    public async Task Loop()
    {
        while (true)
        {
            if (!Connected)
            {
                var source = new CancellationTokenSource();
                source.Cancel();
            }

            if (await _changeService.HasAnyChangesBeenDone()){
                await UpdateTwin();
                await PushChanges();
            }

            await Task.Delay(_deviceInfo.Device["Interval"]);
        }
    }

    private async Task PushChanges()
    {
        if (_logService!.GetList().Any())
            await _logService.PushToDatabaseAsync();

        var changedValues = await _changeService.GetChanged();

        var valuesThatHaveChangedSinceLastRun = ((Dictionary<string, dynamic>) _deviceInfo!.Meta["device"]!)
            .ToList()
            .Where(item => changedValues.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value);

        await _databaseService.UpdateElevator(_deviceInfo.DeviceId, valuesThatHaveChangedSinceLastRun);
        await _changeService.ClearChanges();
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

        Console.WriteLine(
            $"ID:[{_deviceInfo.DeviceId}]\t{_deviceInfo.Device["DeviceName"].ToString()}: I've updated my twin!");
        await _deviceClient.UpdateReportedPropertiesAsync(newTwin);
    }

    private static async Task<TwinCollection> CreateResponse(bool status, string description)
    {
        var twin = new TwinCollection
        {
            ["Value"] = status,
            ["Message"] = description
        };
        return await Task.FromResult(twin);
    }

    private async Task<MethodResponse> ToggleFunctionality(MethodRequest methodrequest, object usercontext)
    {
        Console.WriteLine($"Toggling functionality for elevator {_deviceInfo.DeviceId}");
        var oldValue = _deviceInfo.IsFunctioning;
        var newValue = !oldValue;
        _deviceInfo.IsFunctioning = newValue;

        var description = newValue ? "Elevator is currently online." : "Elevator is currently offline.";
        var eventType = newValue ? "Elevator_Online" : "Elevator_Offline";

        await _logService.AddAsync(description, eventType, oldValue ? "Online" : "Offline",
            newValue ? "Online" : "Offline");

        var (status, message) =
            await _databaseService.SetFunctionalityInDbById(_deviceInfo.DeviceId, newValue.ToString());
        if (!status) return new MethodResponse(Encoding.UTF8.GetBytes(message), 500);

        await _deviceClient.UpdateReportedPropertiesAsync(new TwinCollection
            {["IsFunctioning"] = _deviceInfo.IsFunctioning});
        await _changeService.SetChanged("IsFunctioning");

        Console.WriteLine($"{_deviceInfo.DeviceId}\t{description}");
        return new MethodResponse(
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(newValue, description))), 200);
    }

    private async Task<MethodResponse> OpenCloseDoor(MethodRequest methodRequest, object userContext)
    {
        string keyName = "DoorsAreOpen";
        Console.WriteLine($"Starting OpenCloseDoor for: {_deviceInfo.Device["DeviceName"]}");
        var accident = WillAnAccidentHappen();
        if (accident)
        {
            var responseMessage =
                _repairService.CreateAccident(new List<string> {"Doors are stuck"});
            await _changeService.SetChanged(keyName);
            await PushChanges();
            return new MethodResponse(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false, responseMessage))), 200);
        }

        var toggleDoors = await ToggleDoors();
        var response = await CreateResponse(toggleDoors.Status, toggleDoors.Message);
        return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)),
            toggleDoors.Status ? 200 : 500);
    }

    private async Task<MethodResponse> MoveToFloor(MethodRequest methodRequest, object userContext)
    {
        List<string> accidents = new();
        var keyName = "CurrentFloor";
        if (!_deviceInfo!.IsFunctioning)
            return new MethodResponse(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false,
                    "Method cannot be accessed while the elevator is offline."))), 500);

        FloorChangeRequest request;
        try
        {
            request = JsonConvert.DeserializeObject<FloorChangeRequest>(methodRequest.DataAsJson);
        }
        catch (Exception e)
        {
            return new MethodResponse(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false,
                    "You did not provide any information with the call"))), 500);
        }

        int currentFloor = int.Parse(_deviceInfo.Meta["device"][keyName]);
        var newFloor = request.FloorNumber.Value;

        var errors = new List<string>();

        if (await AreDoorsOpen())
        {
            var accident = WillAnAccidentHappen();
            if (accident) accidents.Add("Doors are stuck");
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
        if (request.FloorNumber <= 0 || request.FloorNumber > _deviceInfo.Device["MaxFloor"])
            errors.Add("You cannot go to that floor.");

        try
        {
            ((Dictionary<string, dynamic>) _deviceInfo!.Meta["type"]!).TryGetValue("MaxWeight", out var typeMaxWeight);
            ((Dictionary<string, dynamic>) _deviceInfo!.Meta["device"]!).TryGetValue("MaxWeight",
                out var deviceMaxWeight);

            if (deviceMaxWeight != null || typeMaxWeight != null)
            {
                int compareWeight = int.Parse(deviceMaxWeight ?? typeMaxWeight);
                //Enhetens maxvikt har företräde före typens maxvikt. Om båda är satta så är det enhetens värde som gäller.
                if (request.WeightAmount > compareWeight)
                    errors.Add(
                        "You exceed the maximum weightlimit set for this elevator. You currently cannot ride this elevator. Please remove some weight.");
            }

            if (errors.Any())
            {
                var returnString = "";
                errors.ForEach(error => returnString += error);
                return new MethodResponse(
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(await CreateResponse(false, returnString))),
                    500);
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

            var relation = Math.Sign(currentFloor.CompareTo(newFloor));
            switch (relation)
            {
                case -1:
                    for (var i = currentFloor; i <= newFloor; i++)
                    {
                        Console.WriteLine($"{_deviceInfo.DeviceId} is currently on floor {i}.");
                        if (!WillAnAccidentHappen()) continue;
                        accidents.Add("Elevator jammed at floor " + i);
                        var responseMessage = _repairService.CreateAccident(accidents);
                        
                        await ChangeMetaValue(keyName, i.ToString());
                        await _changeService!.SetChanged(keyName);
                        
                        _deviceInfo.IsFunctioning = false;

                        await PushChanges();
                        Chalk.Red(responseMessage);
                        return new MethodResponse(
                            Encoding.UTF8.GetBytes(
                                JsonConvert.SerializeObject(await CreateResponse(false, responseMessage))), 200);
                    }

                    break;
                case 1:
                    for (var i = currentFloor; i >= newFloor; i--)
                    {
                        Console.WriteLine($"{_deviceInfo.DeviceId} is currently on floor {i}.");
                        if (!WillAnAccidentHappen()) continue;
                        accidents.Add("Elevator jammed at floor " + i);
                        var responseMessage = _repairService.CreateAccident(accidents);

                        await ChangeMetaValue(keyName, i.ToString());
                        await _changeService!.SetChanged(keyName);
                        
                        _deviceInfo.IsFunctioning = false;
                        
                        await PushChanges();
                        Chalk.Red(responseMessage);
                        return new MethodResponse(
                            Encoding.UTF8.GetBytes(
                                JsonConvert.SerializeObject(await CreateResponse(false, responseMessage))), 200);
                    }

                    break;
            }

            await Task.Delay(500).ContinueWith(_ => _logService!.AddAsync(description, "Elevator Arrived",
                currentFloor.ToString(),
                newFloor.ToString()));

            Console.WriteLine($"{_deviceInfo.DeviceId} stopped at Floor {newFloor}");
            await Task.Delay(500).ContinueWith(_ => ToggleDoors());
            await ChangeMetaValue(keyName, newFloor.ToString());
            await _changeService!.SetChanged(keyName);
            var result = await CreateResponse(false, $"{_deviceInfo.DeviceId} stopped at Floor {newFloor}");
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)), 200);
        }
        catch (Exception e)
        {
            var response = await CreateResponse(false, e.Message);
            return new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)), 500);
        }
    }

    private async Task<MethodResponse> RemoveMetaData(MethodRequest methodRequest, object userContext)
    {
        var standardResponse = (int htmlCode, bool success, string? value, string? message) =>
        {
            var twin = new TwinCollection
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
        try
        {
            request = JsonConvert.DeserializeObject<RemoveMetaDataRequest>(methodRequest.DataAsJson)!;
        }
        catch (Exception e)
        {
            return standardResponse(400, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        var oldValues = new TwinCollection();

        try
        {
            foreach (var key in request.Keys) oldValues[key] = _deviceInfo!.Meta["device"]![key];
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return standardResponse(500, false, "Reset Failed", "Exception Happened: Database was not changed");
        }

        //2. Twinuppdatering
        //3. self check
        try
        {
            foreach (var key in request.Keys) await ChangeMetaValue(key, null);
            await UpdateTwin();
        }
        catch (Exception e)
        {
            return standardResponse(500, false, "Reset Failed", "Exception Happened: " + e.Message);
        }


        //1. Databasanrop
        try
        {
            if (!await _databaseService.RemoveListOfMetaData(DeviceId, request.Keys))
                return standardResponse(500, false, "Reset Failed", "Exception Happened: Database was not changed");
        }
        catch (Exception e)
        {
            return standardResponse(500, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        var description = "Removed Metadata: ";
        request.Keys.ForEach(key => description += key + " ");
        var OldValuesString = "";
        request.Keys.ForEach(key => OldValuesString += $"{key}: {oldValues[key]}");

        await _logService.AddAsync(description, "Metadata Removal", OldValuesString, "null");
        foreach (var key in request.Keys) await _changeService.SetChanged(key);

        return standardResponse(200, true, "Reset Succeded", "Metadata is successfully reset");
    }

    private async Task<MethodResponse> UpdateMetaData(MethodRequest methodRequest, object usercontext)
    {
        var standardResponse = (int htmlCode, bool success, string? value, string? message) =>
        {
            var twin = new TwinCollection
            {
                ["Success"] = success,
                ["Value"] = value,
                ["Message"] = message
            };
            Chalk.Red(twin["Message"]);
            return new MethodResponse(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twin)),
                htmlCode
            );
        };

        UpdateMetaDataRequest request = null!;
        try
        {
            request = JsonConvert.DeserializeObject<UpdateMetaDataRequest>(methodRequest.DataAsJson)!;
            var noKeyExists = string.IsNullOrEmpty(request.Values.Key);
            if (noKeyExists) throw new Exception("No key set! You need a key");
        }
        catch (Exception e)
        {
            Chalk.Red(e.Message);
            return standardResponse(400, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        var oldValue = new KeyValuePair<string, dynamic>();
        try
        {
            if (_deviceInfo.Meta.ContainsKey(request.Values.Key)) oldValue = _deviceInfo.Meta[request.Values.Key];
        }
        catch (Exception e)
        {
            Chalk.Red(e.Message);
        }

        //1. Databasanrop
        try
        {
            if (!await _databaseService.UpdateElevator(_deviceInfo.DeviceId,
                    new Dictionary<string, dynamic> {{request.Values.Key, request.Values.Value}}))
                return standardResponse(500, false, "Reset Failed", "Exception Happened: Database was not changed");
        }
        catch (Exception e)
        {
            Chalk.Red(e.Message);
            return standardResponse(500, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        //2. Twinuppdatering
        //3. self check
        try
        {
            await ChangeMetaValue(request.Values.Key, request.Values.Value);
            await UpdateTwin();
        }
        catch (Exception e)
        {
            Chalk.Red(e.Message);
            return standardResponse(500, false, "Reset Failed", "Exception Happened: " + e.Message);
        }

        var description = "Updated Metadata: ";
        description += $"{oldValue.Key}: ${oldValue.Value} to ";
        description += $"{request.Values.Key}: {request.Values.Value}";
        Chalk.Blue(description);
        await _logService.AddAsync(description, "Metadata Update", description, "null");
        await _changeService.SetChanged(request.Values.Key);
        return standardResponse(200, true, "Update Succeeded", "Metadata is successfully updated");
    }

    public bool IsWorking()
    {
        return _repairService.IsBroken();
    }
}