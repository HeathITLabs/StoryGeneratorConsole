using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;

namespace StoryGenerator.AI.Services
{
    public class ComfyUIImageService : IImageGenerationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ComfyUIImageService> _logger;
        private readonly ResiliencePipeline _retry;
        private readonly string _baseUrl;
        private readonly TimeSpan _timeout;

        public ComfyUIImageService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<ComfyUIImageService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            _baseUrl = config["ComfyUI:BaseUrl"] ?? "http://localhost:8188";
            _timeout = TimeSpan.FromMilliseconds(config.GetValue<int?>("ComfyUI:TimeoutMs") ?? 60000);

            _retry = new ResiliencePipelineBuilder()
                .AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    Delay = TimeSpan.FromSeconds(1),
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential
                })
                .Build();
        }

        public async Task<(byte[] Image, string MimeType)> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt is required", nameof(prompt));

            return await _retry.ExecuteAsync(async ct =>
            {
                using var client = _httpClientFactory.CreateClient(nameof(ComfyUIImageService));
                client.Timeout = _timeout;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var request = BuildSimpleTxt2ImgWorkflow(prompt);

                var postUrl = $"{_baseUrl.TrimEnd('/')}/prompt";
                using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var resp = await client.PostAsync(postUrl, content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"ComfyUI /prompt error {resp.StatusCode}: {body}");

                var post = JsonSerializer.Deserialize<PromptSubmitResponse>(body, JsonOptions)
                           ?? throw new InvalidOperationException("Invalid ComfyUI submit response.");

                // Poll history for result
                var imageInfo = await PollForImageAsync(client, post.prompt_id, ct);
                var imageBytes = await DownloadImageAsync(client, imageInfo.filename, imageInfo.subfolder ?? string.Empty, ct);
                return (imageBytes, "image/png");
            }, cancellationToken);
        }

        private async Task<(string filename, string? subfolder)> PollForImageAsync(HttpClient client, string promptId, CancellationToken ct)
        {
            var historyUrl = $"{_baseUrl.TrimEnd('/')}/history/{promptId}";
            var queueUrl = $"{_baseUrl.TrimEnd('/')}/queue/status";
            var start = DateTime.UtcNow;

            while (true)
            {
                // Optional: read queue status (for progress logs)
                try
                {
                    using var qResp = await client.GetAsync(queueUrl, ct);
                    _ = await qResp.Content.ReadAsStringAsync(ct);
                }
                catch { /* ignore queue errors, continue */ }

                using var resp = await client.GetAsync(historyUrl, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (resp.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
                {
                    var hist = JsonSerializer.Deserialize<ComfyHistoryResponse>(body, JsonOptions);
                    var prompt = hist?.prompts?.Values.FirstOrDefault();
                    var images = prompt?.outputs?.Values.SelectMany(o => o.images ?? Enumerable.Empty<ComfyImage>()).ToList();

                    if (images != null && images.Count > 0)
                    {
                        var img = images.First();
                        return (img.filename ?? throw new InvalidOperationException("Image filename missing."), img.subfolder);
                    }
                }

                if ((DateTime.UtcNow - start) > TimeSpan.FromMinutes(2))
                    throw new TimeoutException("Timed out waiting for ComfyUI result.");

                await Task.Delay(1000, ct);
            }
        }

        private async Task<byte[]> DownloadImageAsync(HttpClient client, string filename, string subfolder, CancellationToken ct)
        {
            var url = $"{_baseUrl.TrimEnd('/')}/view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type=output";
            using var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"/view error {resp.StatusCode}: {body}");
            }
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        private static object BuildSimpleTxt2ImgWorkflow(string prompt)
        {
            // Minimal SD1.5-like workflow graph. Adjust sampler/steps as desired.
            return new
            {
                client_id = Guid.NewGuid().ToString("N"),
                prompt = new Dictionary<string, object>
                {
                    ["1"] = new {
                        inputs = new {
                            ckpt_name = "v1-5-pruned-emaonly.safetensors"
                        },
                        class_type = "CheckpointLoaderSimple",
                        _meta = new { title = "Load Checkpoint" }
                    },
                    ["2"] = new {
                        inputs = new { text = prompt, clip = new object[] { "1", 1 } },
                        class_type = "CLIPTextEncode",
                        _meta = new { title = "Positive" }
                    },
                    ["3"] = new {
                        inputs = new { text = "blurry, low quality, deformed", clip = new object[] { "1", 1 } },
                        class_type = "CLIPTextEncode",
                        _meta = new { title = "Negative" }
                    },
                    ["4"] = new {
                        inputs = new
                        {
                            seed = Random.Shared.NextInt64(),
                            steps = 28,
                            cfg = 7,
                            sampler_name = "euler",
                            scheduler = "normal",
                            denoise = 1,
                            model = new object[] { "1", 0 },
                            positive = new object[] { "2", 0 },
                            negative = new object[] { "3", 0 },
                            latent_image = new object[] { "5", 0 },
                        },
                        class_type = "KSampler",
                        _meta = new { title = "KSampler" }
                    },
                    ["5"] = new {
                        inputs = new { width = 768, height = 512, batch_size = 1 },
                        class_type = "EmptyLatentImage",
                        _meta = new { title = "Empty Latent" }
                    },
                    ["6"] = new {
                        inputs = new { samples = new object[] { "4", 0 }, vae = new object[] { "1", 2 } },
                        class_type = "VAEDecode",
                        _meta = new { title = "VAE Decode" }
                    },
                    ["7"] = new {
                        inputs = new { images = new object[] { "6", 0 } },
                        class_type = "SaveImage",
                        _meta = new { title = "Save Image" }
                    }
                }
            };
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private sealed class PromptSubmitResponse
        {
            public string prompt_id { get; set; } = string.Empty;
        }

        private sealed class ComfyHistoryResponse
        {
            public Dictionary<string, ComfyPrompt>? prompts { get; set; }
        }

        private sealed class ComfyPrompt
        {
            public Dictionary<string, ComfyOutput> outputs { get; set; } = new();
        }

        private sealed class ComfyOutput
        {
            public List<ComfyImage>? images { get; set; }
        }

        private sealed class ComfyImage
        {
            public string? filename { get; set; }
            public string? subfolder { get; set; }
        }
    }
}