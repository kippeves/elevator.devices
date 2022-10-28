using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Amqp.Serialization;
using SmartApp.CLI.Device.Classes;
using SmartApp.CLI.Device.Classes.Base;
using SmartApp.CLI.Device.Models;

namespace Device
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var allDevices = new List<IoTDevice>();
            //allDevices.AddRange(IotDeviceService.GenerateDevices("Kitchen"));
            //allDevices.AddRange(IotDeviceService.GenerateDevices("Living Room"));
            //allDevices.AddRange(IotDeviceService.GenerateDevices("Hallway"));

            var setup = allDevices.Select(device => device.SetupAsync()).ToArray();
            Task.WaitAll(setup);
            Console.WriteLine("Connecting devices.");
            Console.WriteLine("----------");
            allDevices.ForEach(async device => await device.Loop());
            while (true) ;
        }
    }
}