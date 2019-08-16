using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebAPI.Controllers;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var harmony = new Harmony("11");
            var methodInfo = typeof(WeatherForecastController).GetMethod("Get");

            var dynamicMethod = new DynamicMethod("aa", null, null);
            // foreach (var parameterInfo in methodInfo.GetParameters())
            // {
            // var parameter = dynamicMethod.DefineParameter(parameterInfo.Position,  ParameterAttributes.None, parameterInfo.Name);
            // }
            // harmony.Patch(methodInfo, new HarmonyMethod(dynamicMethod));

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
                Host.CreateDefaultBuilder(args)
                        .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });

      
    }
}