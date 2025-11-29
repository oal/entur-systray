using System.Text.Json.Serialization;

namespace EnturSystray;

public record EnturResponse(
    [property: JsonPropertyName("data")] EnturData? Data
);

public record EnturData(
    [property: JsonPropertyName("stopPlace")] StopPlace? StopPlace
);

public record StopPlace(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("quays")] List<Quay>? Quays
);

public record Quay(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("estimatedCalls")] List<EstimatedCall>? EstimatedCalls
);

public record EstimatedCall(
    [property: JsonPropertyName("expectedDepartureTime")] DateTime ExpectedDepartureTime,
    [property: JsonPropertyName("realtime")] bool Realtime,
    [property: JsonPropertyName("destinationDisplay")] DestinationDisplay? DestinationDisplay,
    [property: JsonPropertyName("serviceJourney")] ServiceJourney? ServiceJourney
);

public record DestinationDisplay(
    [property: JsonPropertyName("frontText")] string? FrontText
);

public record ServiceJourney(
    [property: JsonPropertyName("line")] Line? Line
);

public record Line(
    [property: JsonPropertyName("publicCode")] string? PublicCode,
    [property: JsonPropertyName("name")] string? Name
);

// Simplified departure info for the app to use
public record DepartureInfo(
    int MinutesUntilDeparture,
    string LineCode,
    string Destination,
    bool IsRealtime
);

// Geocoder API response models
public record GeocoderResponse(
    [property: JsonPropertyName("features")] List<GeocoderFeature>? Features
);

public record GeocoderFeature(
    [property: JsonPropertyName("properties")] GeocoderProperties? Properties
);

public record GeocoderProperties(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("locality")] string? Locality,
    [property: JsonPropertyName("county")] string? County
);

// Simplified stop info for UI
public record StopInfo(string Id, string Name, string? Locality);

// Simplified quay info for UI
public record QuayInfo(string Id, string Name, List<string> Lines);

// Line and destination info for UI
public record LineDestinationInfo(List<string> Lines, List<string> Destinations);
