using Discord.API;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Discord.Rest;

public sealed class ApiClient : IRestApiClient, IDisposable
{
    private readonly DiscordRestClient _restClient;
    private readonly HttpClient _httpClient;
    internal ApiClient(DiscordRestClient client, DiscordToken token)
    {
        _restClient = client;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(client.Config.APIUrl),
            DefaultRequestHeaders =
            {
                { "Authorization", $"{token.Type} {token.Value}" },
                { "User-Agent",    DiscordConfig.UserAgent       }
            }
        };
    }

    public Task ExecuteAsync(BasicApiRoute route, RequestOptions options, CancellationToken token)
        => SendAsync(
            route,
            new HttpRequestMessage(
                ToHttpMethod(route.Method),
                route.Endpoint
            ),
            options,
            token
        );

    public async Task<T?> ExecuteAsync<T>(ApiRoute<T> route, RequestOptions options, CancellationToken token)
        where T : class
    {
         var result = await SendAsync(
            route,
            new HttpRequestMessage(
                ToHttpMethod(route.Method),
                route.Endpoint
            ),
            options,
            token
         );

         if (result is null)
             return null;

         return await JsonSerializer.DeserializeAsync<T>(result, _restClient.Config.JsonSerializerOptions, token);
    }

    public Task ExecuteAsync<T>(ApiBodyRoute<T> route, RequestOptions options, CancellationToken token)
        where T : class
    {
        var request = new HttpRequestMessage(
            ToHttpMethod(route.Method),
            route.Endpoint
        ) { Content = EncodeBodyContent(route.Body, route.ContentType) };

        return SendAsync(route, request, options, token);
    }

    public async Task<U?> ExecuteAsync<T, U>(ApiBodyRoute<T, U> route, RequestOptions options, CancellationToken token)
        where T : class
        where U : class
    {
        var request = new HttpRequestMessage(
            ToHttpMethod(route.Method),
            route.Endpoint
        ) { Content = EncodeBodyContent(route.Body, route.ContentType) };

        var result = await SendAsync(route, request, options, token);

        if (result is null)
            return null;

        return await JsonSerializer.DeserializeAsync<U>(result, _restClient.Config.JsonSerializerOptions, token);
    }

    private async Task<Stream?> SendAsync(
        ApiRoute route,
        HttpRequestMessage request,
        RequestOptions options,
        CancellationToken token = default)
    {
        if(options.AuditLogReason is not null)
            request.Headers.Add("X-Audit-Log-Reason", Uri.EscapeDataString(options.AuditLogReason));

        _restClient.Logger.LogDebug("Acquiring a bucket ratelimit contract for {}", route);
        var contract = await _restClient.RateLimiter.AcquireContractAsync(route, token);
        _restClient.Logger.LogDebug("Entered bucket contract");

        try
        {
            using var timeoutTokenSource = new CancellationTokenSource(options.Timeout);
            using var requestTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, token);

            // no going back now :D
            _restClient.Logger.LogDebug("Entering critical phase");
            using var phase = await contract.EnterSuperCriticalPhaseDodalooAsync(token);
            _restClient.Logger.LogDebug("Preforming request");

            var response = await _httpClient.SendAsync(request, requestTokenSource.Token);

            _restClient.Logger.LogDebug("{} -> {}", route, response.StatusCode);

            await ProcessRateLimitsAsync(response, contract, token);

            switch (response.StatusCode)
            {
                case >= (HttpStatusCode)200 and < (HttpStatusCode)300:
                    return await response.Content.ReadAsStreamAsync(token);
                case HttpStatusCode.TooManyRequests:
                    contract.Cancel();
                    // retry the request
                    return await SendAsync(route, request, options, token);
                case HttpStatusCode.BadGateway when (_restClient.Config.DefaultRetryMode & RetryMode.Retry502) != 0:
                    // TODO: configure retry
                    await Task.Delay(1000, token);
                    return await SendAsync(route, request, options, token);
                default:
                    throw new HttpRequestException($"Got unsuccessful status code {response.StatusCode}", null,
                        statusCode: response.StatusCode);
            }
        }
        catch
        {
            contract.Cancel();
            throw;
        }
    }

    private ValueTask ProcessRateLimitsAsync(HttpResponseMessage response, RatelimitContract contract,
        CancellationToken token)
    {
        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            return new ValueTask(ProcessGlobalRateLimitAsync(response, token));
        }

        if (
            !response.Headers.Contains("X-RateLimit-Limit") ||
            !response.Headers.Contains("X-RateLimit-Remaining") ||
            !response.Headers.Contains("X-RateLimit-Reset-After") ||
            response.Headers.Date is null)
        {
            // TODO: do we throw or ignore?
            return ValueTask.CompletedTask;
        }

        // TODO: configure how we calculate the reset time
        var resetAt = response.Headers.Date.Value.AddSeconds(
            double.Parse(
                response.Headers.GetValues("X-RateLimit-Reset-After").First()
            )
        );


        contract.Complete(new RatelimitInfo(
            uint.Parse(response.Headers.GetValues("X-RateLimit-Limit").First()),
            uint.Parse(response.Headers.GetValues("X-RateLimit-Remaining").First()),
            resetAt
        ));

        return ValueTask.CompletedTask;
    }

    private async Task ProcessGlobalRateLimitAsync(HttpResponseMessage message,
        CancellationToken token)
    {
        var payload = await ReadRateLimitPayloadAsync(message, token);

        var retryIn = payload?.RetryAfter ?? (message.Headers.TryGetValues("X-Ratelimit-Reset-After", out var r)
            ? double.Parse(r.First())
            : throw new InvalidOperationException("No reset information provided for ratelimit"));

        var retryAt = (message.Headers.Date ?? DateTimeOffset.UtcNow).AddSeconds(retryIn);

        _restClient.RateLimiter.TriggerGlobalLimit(retryAt);
    }

    private Task<Ratelimit?> ReadRateLimitPayloadAsync(HttpResponseMessage message, CancellationToken token)
        => message.Content.ReadFromJsonAsync<Ratelimit?>(_restClient.Config.JsonSerializerOptions, token);

    private HttpContent EncodeBodyContent<T>(T body, ContentType contentType)
    {
        switch (contentType)
        {
            case ContentType.JsonBody:
                return JsonContent.Create(
                    body,
                    options: _restClient.Config.JsonSerializerOptions
                );
            case ContentType.MultipartForm:
                if (body is not IDictionary<string, object?> parts)
                    throw new InvalidCastException("Cannot convert multipart data to dictionary");

                var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture));

                foreach (var part in parts)
                {
                    if(part.Value is null)
                        continue;

                    HttpContent partContent = part.Value switch
                    {
                        string str => new StringContent(str),
                        Stream stream => new StreamContent(stream),
                        _ => throw new InvalidOperationException(
                            $"Unsupported multipart content type '{part.Value.GetType()}'")
                    };

                    content.Add(partContent, part.Key);
                }

                return content;
            default:
                throw new NotImplementedException($"Unimplemented content type '{contentType}'");
        }
    }

    private static HttpMethod ToHttpMethod(RequestMethod method)
    {
        return method switch
        {
            RequestMethod.Get => HttpMethod.Get,
            RequestMethod.Put => HttpMethod.Put,
            RequestMethod.Post => HttpMethod.Post,
            RequestMethod.Delete => HttpMethod.Delete,
            RequestMethod.Patch => HttpMethod.Patch,
            _ => throw new InvalidOperationException()
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
