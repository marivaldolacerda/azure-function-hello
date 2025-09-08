using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace testFunction01
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function("Function1")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string? aStr = req.Query["a"];
            string? bStr = req.Query["b"];

            if (int.TryParse(aStr, out int a) && int.TryParse(bStr, out int b))
            {
                // Converte para "real" dividindo por 100
                double realA = a / 100.0;
                double realB = b / 100.0;

                double soma = realA + realB;

                return new OkObjectResult(
                    $"A soma de {realA} + {realB} = {soma}"
                );
            }
            else
            {
                return new BadRequestObjectResult("Passe dois inteiros válidos escalados (ex: ?a=312&b=-326)");
            }
        }
    }
}
