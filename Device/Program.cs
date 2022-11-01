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
                allElevators.Add(new LowHeightElevator(new DeviceInfo("ebb40d70-7d01-4242-b13a-619dcc8c5bc2")));
                allElevators.Add(new MidHeightElevator(new DeviceInfo("384cbacb-e781-4960-ac36-8dcb162a0a2b")));
            var setup = allElevators.Select(device => device.SetupAsync()).ToArray();
            Task.WaitAll(setup);
            Console.WriteLine("Connecting elevators.");
            Console.WriteLine("----------");
            allElevators.ForEach(async device => await device.Loop());
            while (true) ;
        }
    }
}