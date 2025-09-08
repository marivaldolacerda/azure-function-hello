// ... usings iguais aos seus ...

using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;   // <-- necessário para AppendBlobClient
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace testFunction01
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        public Function1(ILogger<Function1> logger) => _logger = logger;

        public class SensorData
        {
            [JsonPropertyName("deviceId")]
            public string DeviceId { get; set; } = default!;
            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }
            [JsonPropertyName("tmp01")]
            public int Tmp01 { get; set; }
            [JsonPropertyName("tmp02")]
            public int Tmp02 { get; set; }
        }

        private static readonly Regex DeviceIdOk =
            new(@"^[^\/\\#\?\x00-\x1F\x7F]{1,256}$", RegexOptions.Compiled);

        [Function("Function1")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            ILogger log)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<SensorData>(body, opts);
            if (data is null) return new BadRequestObjectResult("JSON inválido");

            if (string.IsNullOrWhiteSpace(data.DeviceId) || !DeviceIdOk.IsMatch(data.DeviceId))
                return new BadRequestObjectResult("deviceId inválido. Evite / \\ # ? e caracteres de controle.");

            if (data.Timestamp.Kind != DateTimeKind.Utc)
                data.Timestamp = DateTime.SpecifyKind(data.Timestamp, DateTimeKind.Utc);

            var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (string.IsNullOrWhiteSpace(conn))
                return new ObjectResult("AzureWebJobsStorage não configurado.") { StatusCode = 500 };

            // ---- Table: grava brutos indexados
            var service = new TableServiceClient(conn);
            var table = service.GetTableClient("telemetry");
            await table.CreateIfNotExistsAsync();

            string rowKey = $"{DateTime.UtcNow.Ticks:d19}-{Guid.NewGuid():N}";
            var entity = new TableEntity(data.DeviceId, rowKey)
            {
                ["TimestampUtc"] = data.Timestamp,
                ["Tmp01"] = data.Tmp01,
                ["Tmp02"] = data.Tmp02
            };
            await table.AddEntityAsync(entity);

            // ---- Blob: log append por dia/dispositivo
            var blobService = new BlobServiceClient(conn);               // <-- agora executa :)
            var container = blobService.GetBlobContainerClient("telemetry-logs");
            await container.CreateIfNotExistsAsync();
            string blobName = $"{data.DeviceId}/{data.Timestamp:yyyy-MM-dd}.log";
            var appendBlob = container.GetAppendBlobClient(blobName);
            if (!await appendBlob.ExistsAsync()) await appendBlob.CreateAsync();
            string line = JsonSerializer.Serialize(data) + "\n";
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(line));
            await appendBlob.AppendBlockAsync(ms);

            // ---- Resposta
            double t1 = data.Tmp01 / 100.0;
            double t2 = data.Tmp02 / 100.0;
            return new OkObjectResult(
                $"Saved: pk={data.DeviceId}, rk={rowKey}\n[{data.Timestamp:u}] tmp01={t1}, tmp02={t2}"
            );
        }
    }
}
