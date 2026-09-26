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

    public OperacionesController(ApplicationDbContext db, ILogger<OperacionesController> logger, AlgoliaBusquedaService algolia)
    {
        _db = db;
        _logger = logger;
        _algolia = algolia;
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

        return RedirectToAction(nameof(Incidencias));
    }

    private Task<List<Incidencia>> ListarAbiertasAsync() =>
        _db.Incidencias.AsNoTracking()
            .Where(i => i.Estado == EstadosIncidencia.Abierta)
            .OrderBy(i => i.Id)
            .ToListAsync();
}
