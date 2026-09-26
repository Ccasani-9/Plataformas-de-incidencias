using Incidencias.Models;
using Microsoft.AspNetCore.Identity;

namespace Incidencias.Data;

public static class SeedData
{
    public const string RolSupervisor = "Supervisor";
    public const string RolOperador = "Operador";

    public static async Task InicializarAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in new[] { RolSupervisor, RolOperador })
        {
            if (!await roles.RoleExistsAsync(rol))
                await roles.CreateAsync(new IdentityRole(rol));
        }

        var config = services.GetRequiredService<IConfiguration>();
        var usuarios = services.GetRequiredService<UserManager<IdentityUser>>();
        await CrearUsuarioAsync(usuarios, "supervisor@incidencias.com",
            config["Seed:SupervisorPassword"] ?? "Supervisor123!", RolSupervisor);
        await CrearUsuarioAsync(usuarios, "operador@incidencias.com",
            config["Seed:OperadorPassword"] ?? "Operador123!", RolOperador);

        if (!db.Incidencias.Any())
        {
            db.Incidencias.AddRange(
                new Incidencia { Id = 1, Estacion = "Plaza San Martín", Descripcion = "Anclaje 4 no libera la bicicleta", Prioridad = "Alta" },
                new Incidencia { Id = 2, Estacion = "Parque Kennedy", Descripcion = "Pantalla del tótem apagada", Prioridad = "Media" },
                new Incidencia { Id = 3, Estacion = "Óvalo Gutiérrez", Descripcion = "Freno delantero defectuoso en bicicleta 112", Prioridad = "Alta" },
                new Incidencia { Id = 4, Estacion = "Malecón Cisneros", Descripcion = "Lector de tarjetas no responde", Prioridad = "Alta" },
                new Incidencia { Id = 5, Estacion = "Plaza de Armas", Descripcion = "Cadena suelta en bicicleta 087", Prioridad = "Baja" },
                new Incidencia { Id = 6, Estacion = "Estadio Nacional", Descripcion = "Anclaje 2 con daño estructural", Prioridad = "Media" },
                new Incidencia { Id = 7, Estacion = "Parque Kennedy", Descripcion = "Llanta trasera pinchada en bicicleta 045", Prioridad = "Baja" },
                new Incidencia { Id = 8, Estacion = "Real Plaza Centro Cívico", Descripcion = "Estación sin conexión a internet", Prioridad = "Alta" },
                new Incidencia { Id = 9, Estacion = "Plaza San Martín", Descripcion = "Luz delantera rota en bicicleta 023", Prioridad = "Baja" },
                new Incidencia { Id = 10, Estacion = "Óvalo Gutiérrez", Descripcion = "Sillín atascado en bicicleta 150", Prioridad = "Media", Estado = EstadosIncidencia.Cerrada });
            await db.SaveChangesAsync();
        }
    }

    private static async Task CrearUsuarioAsync(UserManager<IdentityUser> usuarios, string email, string password, string rol)
    {
        var usuario = await usuarios.FindByEmailAsync(email);
        if (usuario is null)
        {
            usuario = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var resultado = await usuarios.CreateAsync(usuario, password);
            if (!resultado.Succeeded)
                throw new InvalidOperationException(string.Join("; ", resultado.Errors.Select(e => e.Description)));
        }

        if (!await usuarios.IsInRoleAsync(usuario, rol))
            await usuarios.AddToRoleAsync(usuario, rol);
    }
}
