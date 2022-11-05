using Device.Classes.Base;
using Device.Services;

var sql =
    "Server=tcp:kristiansql.database.windows.net,1433;Initial Catalog=azuresql;Persist Security Info=False;User ID=SqlAdmin;Password=9mFZPHjpgoH3KCKwHbmx;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

var dbService = new DatabaseService(sql);
var allElevators = new List<Elevator> {
    new("601baa30-5077-4614-a211-603e09034947", dbService),
    new("08105e08-0765-4ba0-ab14-98ae77f23f3f", dbService)
};

var setup = allElevators.Select(
    device => device.SetupAsync()
).ToArray();

Task.WaitAll(setup);

Console.WriteLine("Connecting elevators.");
Console.WriteLine();

allElevators.ForEach(async device => await device.Loop());

while (true)
{

};