using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;

namespace EnturSystray;

public class EnturService : IDisposable
{
    private const string GraphQlEndpoint = "https://api.entur.io/journey-planner/v3/graphql";
    private const string GeocoderEndpoint = "https://api.entur.io/geocoder/v1/autocomplete";
    private readonly HttpClient _httpClient;

    public EnturService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("ET-Client-Name", "https://github.com/oal/entur-systray");
    }

    public async Task<List<StopInfo>> SearchStopsAsync(string query)
    {
        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var url = $"{GeocoderEndpoint}?text={encodedQuery}&layers=venue&size=10";
            var response = await _httpClient.GetFromJsonAsync<GeocoderResponse>(url);

            if (response?.Features == null)
                return new List<StopInfo>();

            return response.Features
                .Where(f => f.Properties?.Id != null && f.Properties.Id.StartsWith("NSR:StopPlace:"))
                .Select(f => new StopInfo(
                    f.Properties!.Id!,
                    f.Properties.Name ?? "Unknown",
                    f.Properties.Locality
                ))
                .DistinctBy(s => s.Id)
                .ToList();
        }
        catch
        {
            return new List<StopInfo>();
        }
    }

    public async Task<List<QuayInfo>> GetQuaysForStopAsync(string stopPlaceId)
    {
        try
        {
            var query = $$"""
            {
              stopPlace(id: "{{stopPlaceId}}") {
                quays {
                  id
                  name
                  estimatedCalls(numberOfDepartures: 20, timeRange: 7200) {
                    serviceJourney {
                      line {
                        publicCode
                      }
                    }
                  }
                }
              }
            }
            """;

            var requestBody = new { query };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(GraphQlEndpoint, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EnturResponse>();
            if (result?.Data?.StopPlace?.Quays == null)
                return new List<QuayInfo>();

            return result.Data.StopPlace.Quays
                .Select(q => new QuayInfo(
                    q.Id,
                    q.Name ?? q.Id,
                    q.EstimatedCalls?
                        .Select(c => c.ServiceJourney?.Line?.PublicCode)
                        .Where(code => !string.IsNullOrEmpty(code))
                        .Select(code => code!)
                        .Distinct()
                        .OrderBy(code => code)
                        .ToList() ?? new List<string>()
                ))
                .ToList();
        }
        catch
        {
            return new List<QuayInfo>();
        }
    }

    public async Task<LineDestinationInfo> GetLinesAndDestinationsAsync(string stopPlaceId, string? quayId = null)
    {
        try
        {
            var query = $$"""
            {
              stopPlace(id: "{{stopPlaceId}}") {
                quays {
                  id
                  estimatedCalls(numberOfDepartures: 20, timeRange: 7200) {
                    destinationDisplay {
                      frontText
                    }
                    serviceJourney {
                      line {
                        publicCode
                      }
                    }
                  }
                }
              }
            }
            """;

            var requestBody = new { query };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(GraphQlEndpoint, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EnturResponse>();
            if (result?.Data?.StopPlace?.Quays == null)
                return new LineDestinationInfo(new List<string>(), new List<string>());

            var quays = result.Data.StopPlace.Quays;
            if (!string.IsNullOrEmpty(quayId))
                quays = quays.Where(q => q.Id == quayId).ToList();

            var lines = quays
                .SelectMany(q => q.EstimatedCalls ?? new List<EstimatedCall>())
                .Select(c => c.ServiceJourney?.Line?.PublicCode)
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct()
                .OrderBy(code => code)
                .ToList();

            var destinations = quays
                .SelectMany(q => q.EstimatedCalls ?? new List<EstimatedCall>())
                .Select(c => c.DestinationDisplay?.FrontText)
                .Where(dest => !string.IsNullOrEmpty(dest))
                .Distinct()
                .OrderBy(dest => dest)
                .ToList();

            return new LineDestinationInfo(lines!, destinations!);
        }
        catch
        {
            return new LineDestinationInfo(new List<string>(), new List<string>());
        }
    }

    public async Task<List<DepartureInfo>?> GetDeparturesAsync(TrayIconConfig iconConfig)
    {
        try
        {
            var query = BuildQuery(iconConfig.StopPlaceId);
            var requestBody = new { query };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(GraphQlEndpoint, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EnturResponse>();
            return ParseDepartures(result, iconConfig);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<Dictionary<string, List<DepartureInfo>?>> GetDeparturesForAllIconsAsync(List<TrayIconConfig> icons)
    {
        var results = new Dictionary<string, List<DepartureInfo>?>();

        // Group icons by stop place to minimize API calls
        var iconsByStop = icons.GroupBy(i => i.StopPlaceId);

        foreach (var group in iconsByStop)
        {
            var stopPlaceId = group.Key;
            if (string.IsNullOrEmpty(stopPlaceId))
            {
                foreach (var icon in group)
                {
                    results[icon.Id] = null;
                }
                continue;
            }

            try
            {
                var query = BuildQuery(stopPlaceId);
                var requestBody = new { query };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(GraphQlEndpoint, content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<EnturResponse>();

                // Parse departures for each icon using the same API response
                foreach (var icon in group)
                {
                    results[icon.Id] = ParseDepartures(result, icon);
                }
            }
            catch
            {
                foreach (var icon in group)
                {
                    results[icon.Id] = null;
                }
            }
        }

        return results;
    }

    private string BuildQuery(string stopPlaceId)
    {
        return $$"""
        {
          stopPlace(id: "{{stopPlaceId}}") {
            name
            quays {
              id
              name
              estimatedCalls(numberOfDepartures: 10, timeRange: 7200) {
                expectedDepartureTime
                realtime
                destinationDisplay {
                  frontText
                }
                serviceJourney {
                  line {
                    publicCode
                    name
                  }
                }
              }
            }
          }
        }
        """;
    }

    private List<DepartureInfo>? ParseDepartures(EnturResponse? response, TrayIconConfig iconConfig)
    {
        if (response?.Data?.StopPlace?.Quays == null)
            return null;

        var departures = new List<DepartureInfo>();
        var now = DateTime.UtcNow;

        foreach (var quay in response.Data.StopPlace.Quays)
        {
            // Filter by QuayId if set
            if (!string.IsNullOrEmpty(iconConfig.QuayId) && quay.Id != iconConfig.QuayId)
                continue;

            if (quay.EstimatedCalls == null)
                continue;

            foreach (var call in quay.EstimatedCalls)
            {
                var lineCode = call.ServiceJourney?.Line?.PublicCode ?? "";
                var destination = call.DestinationDisplay?.FrontText ?? "";

                // Apply filters
                if (!string.IsNullOrEmpty(iconConfig.LineFilter) && lineCode != iconConfig.LineFilter)
                    continue;

                if (!string.IsNullOrEmpty(iconConfig.DestinationFilter) &&
                    !destination.Contains(iconConfig.DestinationFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var minutesUntil = (int)(call.ExpectedDepartureTime.ToUniversalTime() - now).TotalMinutes;
                if (minutesUntil < 0)
                    continue;

                departures.Add(new DepartureInfo(
                    minutesUntil,
                    lineCode,
                    destination,
                    call.Realtime
                ));
            }
        }

        return departures.OrderBy(d => d.MinutesUntilDeparture).ToList();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
