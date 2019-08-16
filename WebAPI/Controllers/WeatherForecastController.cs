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

            public bool BeforeProcess(InterceptorContext ctx)
            {
                return false;
            }

            public bool AfterProcess(InterceptorContext ctx)
            {
                return false;
            }
        }
    }
}