using System.Net.Http.Json;
using System.Text.Json;

namespace Bot;

public class MidJourney
{
    private const int Timeout = 60 * 1000;

    public class ImagineResponse
    {
        public string? taskId { get; set; }
    }

    public class ResultResponse
    {
        public string? imageURL { get; set; }
        public string? status { get; set; }
        public int? percentage { get; set; }
    }

    public class UpscaleResponse
    {
        public string? imageURL { get; set; }
    }

    public async Task<ImagineResponse> Imagine(string prompt)
    {
        var json = new {prompt};
        try
        {
            return await PostAndGetResponse<ImagineResponse>("https://api.midjourneyapi.io/v2/imagine", json);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error with Imagine MidJourney " + e.Message);
            throw;
        }
    }

    public async Task<ResultResponse> Result(string taskId)
    {
        var json = new {taskId};
        try
        {
            return await PostAndGetResponse<ResultResponse>("https://api.midjourneyapi.io/v2/result", json);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error with Imagine MidJourney " + e.Message);
            throw;
        }
    }

    public async Task<UpscaleResponse> Upscale(string taskId, string position)
    {
        var json = new {taskId, position};
        try
        {
            return await PostAndGetResponse<UpscaleResponse>("https://api.midjourneyapi.io/v2/upscale", json);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error with Imagine MidJourney " + e.Message);
            throw;
        }
    }

    private async Task<T> PostAndGetResponse<T>(string url, object json)
    {
        var client = GetHttpClient();
        var result = await client.PostAsync(url, JsonContent.Create(json));
        if (!result.IsSuccessStatusCode)
            throw new InvalidCastException("Fail to Post, status code: " + result.StatusCode);

        var stringResult = await result.Content.ReadAsStringAsync();
        var resultClass = JsonSerializer.Deserialize<T>(stringResult);
        if (resultClass == null)
            throw new InvalidCastException($"Fail to deserialize, string: [{stringResult}]");

        return resultClass;
    }

    private HttpClient GetHttpClient()
    {
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "9c963efb-0ecd-4227-a5b7-c89c42f31e6a");
        client.DefaultRequestHeaders.Add("ContentType", "application/json");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        return client;
    }
}