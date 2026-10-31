using System.Net.Http.Json;
using System.Text.Json;

namespace UltimateManager.Services;

public sealed class GooglePlaceSearchProvider : IPlaceSearchProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GooglePlaceSearchProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = GoogleApiConfiguration.ApiKey;
    }

    public async Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(_apiKey))
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:autocomplete")
        {
            Content = JsonContent.Create(new { input = query.Trim() }, options: JsonOptions)
        };
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", "suggestions.placePrediction.placeId,suggestions.placePrediction.text.text");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("suggestions", out var suggestions))
        {
            return [];
        }

        return suggestions.EnumerateArray()
            .Select(suggestion => suggestion.TryGetProperty("placePrediction", out var prediction) ? prediction : default)
            .Where(prediction => prediction.ValueKind != JsonValueKind.Undefined &&
                                 prediction.TryGetProperty("placeId", out _) &&
                                 prediction.TryGetProperty("text", out _))
            .Select(prediction => new PlaceSuggestion(
                prediction.GetProperty("placeId").GetString() ?? string.Empty,
                prediction.GetProperty("text").GetProperty("text").GetString() ?? string.Empty))
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.PlaceId) && !string.IsNullOrWhiteSpace(suggestion.Description))
            .ToArray();
    }

    public async Task<LocationResult?> GetLocationAsync(
        string placeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(_apiKey))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}");
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", "id,formattedAddress,location");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = document.RootElement;
        if (!root.TryGetProperty("location", out var location) ||
            !location.TryGetProperty("latitude", out var latitude) ||
            !location.TryGetProperty("longitude", out var longitude))
        {
            return null;
        }

        return new LocationResult(
            latitude.GetDouble(),
            longitude.GetDouble(),
            root.TryGetProperty("formattedAddress", out var address) ? NormalizeAddress(address.GetString()) : string.Empty,
            root.TryGetProperty("id", out var id) ? id.GetString() : placeId);
    }

    public async Task<string?> ResolveAddressAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return null;
        }

        var url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&key={Uri.EscapeDataString(_apiKey)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0 &&
               results[0].TryGetProperty("formatted_address", out var address)
            ? NormalizeAddress(address.GetString())
            : null;
    }

    private static string NormalizeAddress(string? address)
    {
        return string.IsNullOrWhiteSpace(address)
            ? string.Empty
            : string.Join(", ", address.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
