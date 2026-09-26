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
    private readonly CacheIncidenciasService _cache;

    public OperacionesController(ApplicationDbContext db, ILogger<OperacionesController> logger, CacheIncidenciasService cache)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    // GET /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        var abiertas = await ListarAbiertasAsync();
        return View(abiertas);
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

        return RedirectToAction(nameof(Incidencias));
    }

    // Listado general: Redis con expiración de 60 s; si no está en caché se lee de la base.
    private Task<List<Incidencia>> ListarAbiertasAsync() =>
        _cache.ObtenerListadoAsync(() =>
            _db.Incidencias.AsNoTracking()
                .Where(i => i.Estado == EstadosIncidencia.Abierta)
                .OrderBy(i => i.Id)
                .ToListAsync());
}
