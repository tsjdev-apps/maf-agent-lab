using System.ComponentModel;
using GeoTimeZone;
using MAFAgentLab.Core.Helpers;
using OpenWeatherMapSharp;
using OpenWeatherMapSharp.Models;
using OpenWeatherMapSharp.Models.Enums;

namespace MAFAgentLab.Core.Demos;

/// <summary>Provides per-run weather and local-time tools for a demo agent.</summary>
internal sealed class WeatherTools(string apiKey, RunTrace trace)
{
    private readonly OpenWeatherMapService _service = new(apiKey);

    /// <summary>Resolves a location name to its geographic coordinates.</summary>
    private async Task<GeocodeInfo> GetLocationAsync(string location, CancellationToken cancellationToken)
    {
        OpenWeatherMapServiceResponse<List<GeocodeInfo>> response =
            await _service
                .GetLocationByNameAsync(location)
                .WaitAsync(cancellationToken);

        if (!response.IsSuccess || response.Response?.FirstOrDefault() is not { } match)
        {
            throw new InvalidOperationException($"No location result for {location}. Check the location and OpenWeatherMap access.");
        }

        return match;
    }

    /// <summary>Gets current weather for the specified location.</summary>
    [Description("Get the current weather for a city from OpenWeatherMap.")]
    public Task<string> GetWeatherAsync(
        [Description("City and country, for example Pforzheim, Germany.")]
        string location,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            "Weather",
            location,
            async () =>
            {
                GeocodeInfo place =
                    await GetLocationAsync(location, cancellationToken);

                OpenWeatherMapServiceResponse<WeatherRoot> weather =
                    await _service
                        .GetWeatherAsync(
                            place.Latitude,
                            place.Longitude,
                            LanguageCode.EN,
                            Unit.Metric)
                        .WaitAsync(cancellationToken);

                if (!weather.IsSuccess || weather.Response is null)
                {
                    throw new InvalidOperationException(
                        "Live weather could not be retrieved. " +
                        "Check the OpenWeatherMap API key and service availability.");
                }

                return System.Text.Json.JsonSerializer.Serialize(
                    weather.Response);
            },
            cancellationToken);

    /// <summary>Gets the current local time for the specified location.</summary>
    [Description("Get the current local time for a city using geocoding and its time zone.")]
    public Task<string> GetCurrentTimeAsync(
        [Description("City and country.")]
        string location,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            "LocalTime",
            location,
            async () =>
            {
                GeocodeInfo place =
                    await GetLocationAsync(location, cancellationToken);

                TimeZoneResult zone =
                    TimeZoneLookup.GetTimeZone(
                        place.Latitude,
                        place.Longitude);

                IEnumerable<string> zoneIds =
                    new[] { zone.Result }
                        .Concat(zone.AlternativeResults)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct();

                foreach (string id in zoneIds)
                {
                    try
                    {
                        TimeZoneInfo timeZone =
                            TimeZoneInfo.FindSystemTimeZoneById(id);

                        DateTimeOffset time =
                            TimeZoneInfo.ConvertTime(
                                DateTimeOffset.UtcNow,
                                timeZone);

                        return
                            $"{location}: " +
                            $"{time:yyyy-MM-dd HH:mm:ss zzz} ({id})";
                    }
                    catch (TimeZoneNotFoundException)
                    {
                    }
                    catch (InvalidTimeZoneException)
                    {
                    }
                }

                throw new InvalidOperationException(
                    $"No supported time zone found for {location}.");
            },
            cancellationToken);

    /// <summary>Executes a tool call while recording its trace state.</summary>
    private async Task<string> ExecuteAsync(string name, string location, Func<Task<string>> execute, CancellationToken cancellationToken)
    {
        string actor = $"Tool | {name}";
        trace.Start(actor);
        trace.Event(actor, $"location: {location}");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string result = await execute();
            trace.Complete(actor, result);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            trace.Cancel(actor, "cancelled");
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Do not return HTTP exception URLs: weather query strings can contain credentials.
            string message = error is InvalidOperationException ? error.Message : "The weather service is unreachable or timed out.";
            trace.Fail(actor, message);
            return $"Tool error: {message}";
        }
    }
}
