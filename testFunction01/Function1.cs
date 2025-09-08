using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace testFunction01
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        public class SensorData
        {
            public DateTime timestamp { get; set; }
            public int tmp01 { get; set; }
            public int tmp02 { get; set; }
        }

        [Function("Function1")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<SensorData>(body);

            if (data == null)
                return new BadRequestObjectResult("JSON inválido");

            // Converte para valor real (dividindo por 100)
            double t1 = data.tmp01 / 100.0;
            double t2 = data.tmp02 / 100.0;

            return new OkObjectResult(
                $"[{data.timestamp:u}] tmp01={t1}, tmp02={t2}"
            );
        }
    }
}
