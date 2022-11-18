using Device.Classes.Base;
using Device.Services;

var sql =
    "Server=tcp:kristiansql.database.windows.net,1433;Initial Catalog=azuresql;Persist Security Info=False;User ID=SqlAdmin;Password=9mFZPHjpgoH3KCKwHbmx;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

var dbService = new DatabaseService(sql);
async void Loop(Elevator device) => await device.Loop();




var elevators = await dbService.GetListOfElevatorIds();
var allElevators = elevators.Select(
    elevatorId => 
        new Elevator(
            elevatorId, 
            dbService)
    ).ToList();

var setup = allElevators.Select(
    device => device.SetupAsync()
).ToArray();

Task.WaitAll(setup);
Console.WriteLine($"{allElevators.Count} elevators are online");
allElevators.ForEach(Loop);
while (true);