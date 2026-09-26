using Incidencias.Data;
using Incidencias.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Incidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(ApplicationDbContext db, ILogger<OperacionesController> logger)
    {
        _db = db;
        _logger = logger;
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

        return RedirectToAction(nameof(Incidencias));
    }

    private Task<List<Incidencia>> ListarAbiertasAsync() =>
        _db.Incidencias.AsNoTracking()
            .Where(i => i.Estado == EstadosIncidencia.Abierta)
            .OrderBy(i => i.Id)
            .ToListAsync();
}
