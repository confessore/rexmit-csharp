// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using rexmit.Models.GPT;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public sealed class GPTService : IGPTService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GPTService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> RequestGPTChatCompletionAsync(string prompt)
    {
        var data = new
        {
            model = "gpt-3.5-turbo",
            //model = "orca-mini-3b.ggmlv3.q4_0.bin",
            //prompt,
            //max_tokens = 750,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.5
        };
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpClient = _httpClientFactory.CreateClient(nameof(GPTService));
        try
        {
            var response = await httpClient.PostAsync(string.Empty, content);
            if (response.IsSuccessStatusCode)
            {
                json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
                var gpt = JsonSerializer.Deserialize<GPTChatCompletion>(json, options);
                var text = gpt.Choices[0].Message.Content;
                return text;
            }
            else
            {
                return $"Request failed with status code {response.StatusCode}";
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
