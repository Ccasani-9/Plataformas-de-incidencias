using System.Net.Http.Json;
using System.Text.Json;
using Incidencias.Models;

namespace Incidencias.Services;

/// <summary>
/// Cliente REST de Algolia usado solo desde el servidor. Las claves se leen de
/// variables de entorno (Algolia__ApplicationId, Algolia__SearchApiKey, Algolia__AdminApiKey)
/// y nunca se envían al navegador.
/// </summary>
public class AlgoliaBusquedaService
{
    // JsonContent usa camelCase por defecto; Algolia distingue mayúsculas en los nombres
    // de atributos, así que se serializa con los nombres exactos (Estacion, Descripcion...).
    private static readonly JsonSerializerOptions NombresExactos = new();

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AlgoliaBusquedaService> _logger;

    public AlgoliaBusquedaService(HttpClient http, IConfiguration config, ILogger<AlgoliaBusquedaService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private string AppId => _config["Algolia:ApplicationId"] ?? string.Empty;
    private string IndexName => _config["Algolia:IndexName"] is { Length: > 0 } nombre ? nombre : "incidencias";

    public bool Configurado => AppId.Length > 0 && !string.IsNullOrEmpty(_config["Algolia:SearchApiKey"]);

    /// <summary>Devuelve los Id de las incidencias que coinciden con el texto, en el orden de relevancia de Algolia.</summary>
    public async Task<List<int>> BuscarIdsAsync(string texto, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new InvalidOperationException("Algolia no está configurado (Algolia__ApplicationId / Algolia__SearchApiKey).");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://{AppId}-dsn.algolia.net/1/indexes/{Uri.EscapeDataString(IndexName)}/query");
        request.Headers.Add("X-Algolia-Application-Id", AppId);
        request.Headers.Add("X-Algolia-API-Key", _config["Algolia:SearchApiKey"]);
        request.Content = JsonContent.Create(new { query = texto, hitsPerPage = 100 }, options: NombresExactos);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var ids = new List<int>();
        foreach (var hit in json.RootElement.GetProperty("hits").EnumerateArray())
        {
            if (ObtenerId(hit) is int id)
                ids.Add(id);
        }

        _logger.LogInformation("Algolia: búsqueda '{Texto}' devolvió {Total} coincidencias", texto, ids.Count);
        return ids;
    }

    /// <summary>
    /// Carga (o actualiza) las incidencias en el índice. Solo se ejecuta si existe
    /// Algolia__AdminApiKey en el servidor; la clave de administración no sale del backend.
    /// </summary>
    public async Task IndexarAsync(IEnumerable<Incidencia> incidencias, CancellationToken ct = default)
    {
        var adminKey = _config["Algolia:AdminApiKey"];
        if (AppId.Length == 0 || string.IsNullOrEmpty(adminKey))
            return;

        var baseUrl = $"https://{AppId}.algolia.net/1/indexes/{Uri.EscapeDataString(IndexName)}";

        using (var settings = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/settings"))
        {
            settings.Headers.Add("X-Algolia-Application-Id", AppId);
            settings.Headers.Add("X-Algolia-API-Key", adminKey);
            settings.Content = JsonContent.Create(new { searchableAttributes = new[] { "Estacion", "Descripcion" } }, options: NombresExactos);
            (await _http.SendAsync(settings, ct)).EnsureSuccessStatusCode();
        }

        var requests = incidencias.Select(i => new
        {
            action = "updateObject",
            body = new
            {
                objectID = i.Id.ToString(),
                i.Id,
                i.Estacion,
                i.Descripcion,
                i.Prioridad,
                i.Estado
            }
        }).ToList();

        using var batch = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/batch");
        batch.Headers.Add("X-Algolia-Application-Id", AppId);
        batch.Headers.Add("X-Algolia-API-Key", adminKey);
        batch.Content = JsonContent.Create(new { requests }, options: NombresExactos);
        (await _http.SendAsync(batch, ct)).EnsureSuccessStatusCode();

        _logger.LogInformation("Algolia: {Total} incidencias indexadas en '{Indice}'", requests.Count, IndexName);
    }

    private static int? ObtenerId(JsonElement hit)
    {
        foreach (var nombre in new[] { "objectID", "Id", "id" })
        {
            if (!hit.TryGetProperty(nombre, out var valor))
                continue;
            if (valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var n))
                return n;
            if (valor.ValueKind == JsonValueKind.String && int.TryParse(valor.GetString(), out var s))
                return s;
        }
        return null;
    }
}
