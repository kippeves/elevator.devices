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
            
            allElevators.Add(new LowHeightElevator(new DeviceInfo("4092b9f8-3183-426f-b8f6-d66721e09da1")));
            allElevators.Add(new MidHeightElevator(new DeviceInfo("7910771f-5188-4973-8d0e-e854824b0160")));

            var setup = allElevators.Select(device => device.SetupAsync()).ToArray();
            Task.WaitAll(setup);
            Console.WriteLine("Connecting elevators.");
            Console.WriteLine("----------");
            allElevators.ForEach(async device => await device.Loop());
            while (true) ;
        }
    }
}