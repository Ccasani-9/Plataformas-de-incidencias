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
    private readonly PieHostPublisher _pieHost;

    public OperacionesController(ApplicationDbContext db, ILogger<OperacionesController> logger, PieHostPublisher pieHost)
    {
        _db = db;
        _logger = logger;
        _pieHost = pieHost;
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

    private Task<List<Incidencia>> ListarAbiertasAsync() =>
        _db.Incidencias.AsNoTracking()
            .Where(i => i.Estado == EstadosIncidencia.Abierta)
            .OrderBy(i => i.Id)
            .ToListAsync();
}
