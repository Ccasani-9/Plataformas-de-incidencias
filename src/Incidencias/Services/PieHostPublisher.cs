using System.Net.Http.Json;

namespace Incidencias.Services;

/// <summary>
/// Publica eventos en el canal WebSocket de PieHost (PieSocket) desde el servidor.
/// Variables de entorno: PieHost__ClusterId, PieHost__ApiKey, PieHost__ApiSecret, PieHost__Channel.
/// El secreto solo se usa aquí; el navegador recibe únicamente la API key pública.
/// </summary>
public class PieHostPublisher
{
    public const string EventoIncidenciaActualizada = "IncidenciaActualizada";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PieHostPublisher> _logger;

    public PieHostPublisher(HttpClient http, IConfiguration config, ILogger<PieHostPublisher> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    // Acepta "s1234.blr1" o el host completo "s1234.blr1.piesocket.com".
    public static string Host(string clusterId) =>
        string.IsNullOrWhiteSpace(clusterId) ? string.Empty
        : clusterId.EndsWith(".com", StringComparison.OrdinalIgnoreCase) ? clusterId
        : $"{clusterId}.piesocket.com";

    public static string Canal(IConfiguration config) =>
        config["PieHost:Channel"] is { Length: > 0 } canal ? canal : "incidencias";

    public async Task PublicarIncidenciaActualizadaAsync(int id, string estado, CancellationToken ct = default)
    {
        var cluster = _config["PieHost:ClusterId"];
        var apiKey = _config["PieHost:ApiKey"];
        var secret = _config["PieHost:ApiSecret"];
        if (string.IsNullOrEmpty(cluster) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("PieHost no configurado; no se publicó {Evento} para la incidencia {Id}", EventoIncidenciaActualizada, id);
            return;
        }

        var canal = Canal(_config);
        var cuerpo = new
        {
            key = apiKey,
            secret,
            roomId = canal,
            channelId = canal,
            message = new
            {
                @event = EventoIncidenciaActualizada,
                data = new { Id = id, Estado = estado }
            }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync($"https://{Host(cluster)}/api/publish", cuerpo, ct);
            var respuesta = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("PieHost: publicado {Evento} {{Id={Id}, Estado={Estado}}} en canal {Canal}", EventoIncidenciaActualizada, id, estado, canal);
            else
                _logger.LogError("PieHost respondió {Status}: {Respuesta}", (int)response.StatusCode, respuesta);
        }
        catch (Exception ex)
        {
            // El cambio ya está persistido; un fallo de publicación no debe revertir el cierre.
            _logger.LogError(ex, "Error publicando en PieHost la incidencia {Id}", id);
        }
    }
}
