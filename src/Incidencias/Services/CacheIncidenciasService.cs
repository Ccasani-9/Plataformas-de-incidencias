using System.Text.Json;
using Incidencias.Models;
using StackExchange.Redis;

namespace Incidencias.Services;

/// <summary>
/// Caché en Redis del listado general de incidencias abiertas (60 segundos).
/// La búsqueda con texto (Algolia) no usa esta caché.
/// </summary>
public class CacheIncidenciasService
{
    public const string ClaveListado = "incidencias:abiertas";
    public static readonly TimeSpan Expiracion = TimeSpan.FromSeconds(60);

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<CacheIncidenciasService> _logger;

    public CacheIncidenciasService(ILogger<CacheIncidenciasService> logger, IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<List<Incidencia>> ObtenerListadoAsync(Func<Task<List<Incidencia>>> cargarDesdeBase)
    {
        if (_redis is null)
        {
            _logger.LogInformation("Listado leído desde la BASE DE DATOS (Redis no configurado)");
            return await cargarDesdeBase();
        }

        var redisDb = _redis.GetDatabase();
        try
        {
            var valor = await redisDb.StringGetAsync(ClaveListado);
            if (valor.HasValue)
            {
                var desdeCache = JsonSerializer.Deserialize<List<Incidencia>>(valor.ToString());
                if (desdeCache is not null)
                {
                    _logger.LogInformation("Listado leído desde REDIS (hit, clave {Clave})", ClaveListado);
                    return desdeCache;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer Redis; se consulta la base de datos");
        }

        var listado = await cargarDesdeBase();
        _logger.LogInformation("Listado leído desde la BASE DE DATOS (miss en Redis, se guarda {Segundos}s)", Expiracion.TotalSeconds);

        try
        {
            await redisDb.StringSetAsync(ClaveListado, JsonSerializer.Serialize(listado), Expiracion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el listado en Redis");
        }

        return listado;
    }

    public async Task InvalidarAsync()
    {
        if (_redis is null)
            return;

        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(ClaveListado);
            _logger.LogInformation("Redis: clave {Clave} invalidada", ClaveListado);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar la clave {Clave} en Redis", ClaveListado);
        }
    }

    /// <summary>
    /// Acepta tanto el formato de StackExchange.Redis ("host:puerto,password=...")
    /// como URLs redis:// o rediss:// (Render Key Value, Upstash, Redis Cloud).
    /// </summary>
    public static ConfigurationOptions CrearOpciones(string cadena)
    {
        ConfigurationOptions opciones;
        if (cadena.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            cadena.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(cadena);
            opciones = new ConfigurationOptions
            {
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
            };
            opciones.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var partes = uri.UserInfo.Split(':', 2);
                if (partes.Length == 2)
                {
                    opciones.User = Uri.UnescapeDataString(partes[0]);
                    opciones.Password = Uri.UnescapeDataString(partes[1]);
                }
                else
                {
                    opciones.Password = Uri.UnescapeDataString(partes[0]);
                }
            }
        }
        else
        {
            opciones = ConfigurationOptions.Parse(cadena);
        }

        opciones.AbortOnConnectFail = false;
        return opciones;
    }
}
