using Incidencias.Data;
using Incidencias.Models;
using Incidencias.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Incidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OperacionesController> _logger;
    private readonly AlgoliaBusquedaService _algolia;
    private readonly CacheIncidenciasService _cache;
    private readonly PieHostPublisher _pieHost;

    public OperacionesController(ApplicationDbContext db, ILogger<OperacionesController> logger,
        AlgoliaBusquedaService algolia, CacheIncidenciasService cache, PieHostPublisher pieHost)
    {
        _db = db;
        _logger = logger;
        _algolia = algolia;
        _cache = cache;
        _pieHost = pieHost;
    }

    // GET /Operaciones/Incidencias?q=texto
    public async Task<IActionResult> Incidencias(string? q)
    {
        ViewData["Busqueda"] = q;

        if (string.IsNullOrWhiteSpace(q))
        {
            var abiertas = await ListarAbiertasAsync();
            return View(abiertas);
        }

        return View(await BuscarAbiertasAsync(q.Trim()));
    }

    // El servidor consulta Algolia y solo muestra incidencias que existen en la base y siguen abiertas.
    private async Task<List<Incidencia>> BuscarAbiertasAsync(string texto)
    {
        List<int> ids;
        try
        {
            ids = await _algolia.BuscarIdsAsync(texto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando Algolia");
            ViewData["ErrorBusqueda"] = "No se pudo consultar el índice de búsqueda.";
            return new List<Incidencia>();
        }

        var encontradas = await _db.Incidencias.AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.Estado == EstadosIncidencia.Abierta)
            .ToListAsync();

        // Mantener el orden de relevancia devuelto por Algolia.
        return encontradas.OrderBy(i => ids.IndexOf(i.Id)).ToList();
    }

    // POST /Operaciones/Cerrar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SeedData.RolSupervisor)]
    public async Task<IActionResult> Cerrar(int id)
    {
        var incidencia = await _db.Incidencias.FindAsync(id);
        if (incidencia is null)
            return NotFound();

        incidencia.Estado = EstadosIncidencia.Cerrada;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Incidencia {Id} cerrada en la base de datos", id);

        // Invalidar el listado cacheado antes de volver a consultarlo.
        await _cache.InvalidarAsync();

        // Publicar solo después de persistir el nuevo estado.
        await _pieHost.PublicarIncidenciaActualizadaAsync(incidencia.Id, incidencia.Estado);

        return RedirectToAction(nameof(Incidencias));
    }

    // GET /Operaciones/EstadoIncidencias — estado vigente, consultado por la pantalla al reconectar el WebSocket.
    [HttpGet]
    public async Task<IActionResult> EstadoIncidencias()
    {
        var abiertas = await _db.Incidencias.AsNoTracking()
            .Where(i => i.Estado == EstadosIncidencia.Abierta)
            .Select(i => new { i.Id, i.Estado })
            .ToListAsync();
        return Json(abiertas);
    }

    // Listado general: Redis con expiración de 60 s; si no está en caché se lee de la base.
    private Task<List<Incidencia>> ListarAbiertasAsync() =>
        _cache.ObtenerListadoAsync(() =>
            _db.Incidencias.AsNoTracking()
                .Where(i => i.Estado == EstadosIncidencia.Abierta)
                .OrderBy(i => i.Id)
                .ToListAsync());
}
