using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicProxy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [Uow]
        public string Get(string s)
        {
            return xx();
        }

        private static string xx()
        {
            Console.WriteLine("DDD");
            return "1";
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class UowAttribute : InterceptorAttribute
    {
        public UowAttribute() : base(typeof(MyClass))
        {
        }

        private class MyClass : IInterceptor
        {
            public MyClass()
            {
            }

            public InterceptControl BeforeProcess(InterceptorContext ctx)
            {
                Console.WriteLine(ctx.Target);
                ctx.ReturnValue.SetValue("000");
                ctx.Context["a"] = 1;
                return InterceptControl.SkipAll;
            }

            public InterceptControl AfterProcess(InterceptorContext ctx)
            {
                Console.WriteLine(ctx.Target);
                foreach (var o in ctx.Parameters.Select(e=>e.GetValue()))
                {
                    Console.WriteLine(o);
                }
                Console.WriteLine(ctx.ReturnValue.GetValue());
                Console.WriteLine(ctx.Context["a"] + "0");
                ctx.ReturnValue.SetValue("AAAA");
                return InterceptControl.None;
            }
        }
    }
}