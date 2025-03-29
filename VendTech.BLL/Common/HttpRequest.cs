using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Configuration;

public class ReliableHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetries;
    private readonly int _timeoutSeconds;

    public ReliableHttpClient(int maxRetries = 3, int timeoutSeconds = 340)
    {
        _httpClient = new HttpClient();
        _maxRetries = maxRetries;
        _timeoutSeconds = timeoutSeconds;

        // Load API Key from Web.config
        string apiKey = WebConfigurationManager.AppSettings["ApiKey"]?.ToString();
        if (!string.IsNullOrEmpty(apiKey) && !_httpClient.DefaultRequestHeaders.Contains("X-Api-Key"))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }

    public async Task<string> SendPostRequestAsync(string url, string jsonBody)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds)))
                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content })
                {
                    Console.WriteLine($"Attempt {attempt}: Sending POST request to {url}");

                    HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Success on attempt {attempt}");
                        return await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        Console.WriteLine($"HTTP {response.StatusCode}. Retrying...");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"Request timeout on attempt {attempt}. Retrying...");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error: {ex.Message}. Retrying...");
            }

            await Task.Delay(ComputeBackoffDelay(attempt));
        }

        throw new Exception($"POST request failed after {_maxRetries} attempts.");
    }

    private TimeSpan ComputeBackoffDelay(int attempt)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff (2s, 4s, 8s)
    }
}
