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

int broken = 0, working = 0;
var setup = allElevators.Select(
    async device =>
    {
        await device.SetupAsync();
        if (device.IsWorking())
            working++;
        else broken++;
    }).ToArray();

Task.WaitAll(setup);
Chalk.Gray($"{working} elevator"+(working==1?" is":"s are")+" online.");
Chalk.Gray($"{broken} elevator"+(broken==1?" is":"s are")+" currently offline due to errors.");
allElevators.ForEach(Loop);
while (true);