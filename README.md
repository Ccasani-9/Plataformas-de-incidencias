# Plataforma de Incidencias

ASP.NET Core MVC + Identity, EF Core y SQLite. Registro de averías en estaciones de bicicletas compartidas.

## Ejecutar localmente

```bash
dotnet run --project src/Incidencias
```

Pantalla principal: `/Operaciones/Incidencias`

## Usuarios de prueba

| Usuario | Contraseña | Rol |
|---|---|---|
| supervisor@incidencias.com | Supervisor123! | Supervisor (puede cerrar incidencias) |
| operador@incidencias.com | Operador123! | Operador (solo consulta) |

## Despliegue

Render (Web Service, runtime Docker) despliega la rama `main` usando el `Dockerfile` de la raíz.
