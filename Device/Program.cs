using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.CompilerServices;
using Device.Classes;
using Device.Classes.Base;
using Microsoft.Azure.Amqp.Serialization;
using SmartApp.CLI.Device.Models;

namespace Device
{
    public static class Program
    {
        public static Task Main(string[] args)
        {
            var allElevators = new List<Elevator>();
                allElevators.Add(new LowHeightElevator(new DeviceInfo("601baa30-5077-4614-a211-603e09034947")));
                allElevators.Add(new MidHeightElevator(new DeviceInfo("08105e08-0765-4ba0-ab14-98ae77f23f3f")));
            var setup = allElevators.Select(device => device.SetupAsync()).ToArray();
            Task.WaitAll(setup);
            Console.WriteLine("Connecting elevators.");
            Console.WriteLine("----------");
            allElevators.ForEach(async device => await device.Loop());
            while (true) ;
        }
    }
}