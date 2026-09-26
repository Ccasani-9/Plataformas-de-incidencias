# Plataforma de Incidencias

Registro de averías en estaciones de bicicletas compartidas.

- **Stack:** ASP.NET Core 8 MVC + Identity, EF Core y SQLite
- **Servicios:** búsqueda con Algolia, caché con Redis y WebSocket con PieHost (PieSocket)
- **Infraestructura:** Render.com (Web Service, runtime Docker), que despliega la rama `main`
- **Pantalla principal:** `/Operaciones/Incidencias`

## Entrega

| Elemento | Valor |
|---|---|
| Repositorio | https://github.com/Ccasani-9/Plataformas-de-incidencias |
| PR A (Algolia) | https://github.com/Ccasani-9/Plataformas-de-incidencias/pull/1 |
| PR B (Redis) | https://github.com/Ccasani-9/Plataformas-de-incidencias/pull/2 |
| PR C (PieHost) | https://github.com/Ccasani-9/Plataformas-de-incidencias/pull/3 |
| URL de Render | https://plataformas-de-incidencias.onrender.com |
| Commit desplegado | El último commit de `main`. Se muestra en el pie de cada página (`RENDER_GIT_COMMIT`) y en Render → *Events*. Se comprueba con `git rev-parse --short origin/main`. |
| PR adicionales | #4 (README de entrega), #5 y #6 (correcciones detectadas al probar en producción, ver más abajo) |

## Usuarios de prueba

| Usuario | Contraseña | Rol |
|---|---|---|
| supervisor@incidencias.com | Supervisor123! | Supervisor (puede cerrar incidencias) |
| operador@incidencias.com | Operador123! | Operador (solo consulta) |

Las contraseñas se pueden cambiar con `Seed__SupervisorPassword` y `Seed__OperadorPassword`.

## Configuración por variables de entorno

El repositorio **no contiene claves**: en `appsettings.json` esos campos están vacíos. En Render se configuran en *Environment*. ASP.NET Core traduce `__` a `:`.

| Variable | Uso | ¿Llega al navegador? |
|---|---|---|
| `Algolia__ApplicationId` | Aplicación de Algolia | No |
| `Algolia__SearchApiKey` | Consultas de búsqueda desde el servidor | No |
| `Algolia__AdminApiKey` | Solo el servidor la usa, para cargar el índice al arrancar | **No, nunca** |
| `Algolia__IndexName` | Nombre del índice (`incidencias`) | No |
| `Redis__ConnectionString` | `redis://default:PASSWORD@HOST:PUERTO` (también acepta `rediss://` o el formato de StackExchange.Redis) | No |
| `PieHost__ClusterId` | Cluster de PieSocket, p. ej. `s1234.blr1` | Sí (necesario para abrir el WebSocket) |
| `PieHost__ApiKey` | API key pública del canal | Sí (necesario para abrir el WebSocket) |
| `PieHost__ApiSecret` | Secreto para publicar desde el servidor | **No, nunca** |
| `PieHost__Channel` | Canal WebSocket (`incidencias`) | Sí |

`PORT` y `RENDER_GIT_COMMIT` los inyecta Render automáticamente.

## Funcionalidades

### A. Búsqueda con Algolia (`feature/busqueda-algolia`)
- `GET /Operaciones/Incidencias?q=texto`: el **servidor** consulta Algolia por estación o descripción (`AlgoliaBusquedaService`, API REST).
- Con los `objectID` devueltos se filtra en SQLite. Solo se muestran incidencias **que existen en la base y siguen abiertas**, así que una incidencia cerrada no vuelve a aparecer aunque el índice aún la contenga.
- Con la búsqueda vacía se muestra el listado habitual.
- Al arrancar, si existe `Algolia__AdminApiKey`, el servidor carga las incidencias de prueba en el índice. `objectID` corresponde al `Id` de la base.

### B. Caché con Redis (`feature/cache-redis`)
- El listado general se guarda en la clave `incidencias:abiertas` con una expiración de **60 s**.
- La búsqueda con texto no usa la caché: va directo a Algolia y a la base.
- Al cerrar una incidencia se guarda en la base y **se invalida la clave** antes de volver a consultar el listado.
- Los logs indican el origen de cada lectura:
  - `Listado leído desde la BASE DE DATOS (miss en Redis, se guarda 60s)`
  - `Listado leído desde REDIS (hit, clave incidencias:abiertas)`
  - `Redis: clave incidencias:abiertas invalidada`

### C. Actualización en tiempo real con PieHost (`feature/websocket-piehost`)
- Al cerrar una incidencia primero se **persiste** el estado. Después el servidor publica el evento `IncidenciaActualizada` con `{ Id, Estado }` en PieHost (`POST https://{cluster}.piesocket.com/api/publish`).
- La pantalla se conecta a `wss://{cluster}.piesocket.com/v3/{canal}`. Al recibir el evento quita la fila y actualiza el total **sin recargar la página**.
- Al **reconectar**, la pantalla consulta el estado vigente en `GET /Operaciones/EstadoIncidencias` y retira las incidencias que ya no están abiertas.

## Ramas, fusiones y conflictos

Las ramas A, B y C nacen del mismo commit inicial de `main` (`cc3d5eb`). Las tres cambiaron la misma línea `<h1>Incidencias abiertas</h1>`. Las fusiones se hicieron en orden A → B → C, con merge commits (sin squash ni force push).

```
*   7c04d2d Merge pull request #3 from Ccasani-9/feature/websocket-piehost
|\
| *   510727e Merge main en feature/websocket-piehost: resolver conflicto con búsqueda y caché   ← resolución 2
| |\
| |/
|/|
* |   cd29ba6 Merge pull request #2 from Ccasani-9/feature/cache-redis
|\ \
| * \   4872fa2 Merge main en feature/cache-redis: resolver conflicto con búsqueda Algolia     ← resolución 1
| |\ \
| |/ /
|/| |
* | |   d41dd63 Merge pull request #1 from Ccasani-9/feature/busqueda-algolia
|\ \ \
| * | | ee65ac6 feat(busqueda): búsqueda de incidencias abiertas con Algolia desde el servidor   ← A
|/ / /
| * / 9681e62 feat(cache): listado de incidencias abiertas en Redis (60 s) con invalidación al cerrar   ← B
|/ /
| * d8ae868 feat(tiempo-real): publicar IncidenciaActualizada en PieHost y actualizar la lista por WebSocket   ← C
|/
* cc3d5eb Proyecto base (ancestro común)
```

Para verlo: `git log --graph --oneline --all`

Después de las tres funcionalidades se fusionaron, también por PR y sin tocar `main` directamente:
- **PR #4:** README de entrega y commit desplegado en el pie de página.
- **PR #5:** en producción la búsqueda devolvía 0 resultados. `JsonContent` serializa en camelCase, así que los registros llegaban a Algolia como `estacion`/`descripcion`, mientras los atributos buscables eran `Estacion`/`Descripcion`.
- **PR #6:** por la misma causa, el evento se publicaba como `{"id":..,"estado":..}`. Ahora se publica con `Id` y `Estado`, como pide el enunciado. Además, la pantalla muestra "No hay incidencias abiertas" cuando el WebSocket retira la última fila.

### Resolución 1 (`4872fa2`): main (con A) incorporado en B
Conflictos en `OperacionesController.cs` y `Incidencias.cshtml`.
- **Controlador:** el constructor recibe **ambos** servicios, `AlgoliaBusquedaService` y `CacheIncidenciasService`. Sin texto de búsqueda, el listado sale de Redis o de la base. Con texto, se consulta Algolia sin pasar por la caché.
- **Vista:** se conserva el formulario de búsqueda de A. El título queda "Incidencias abiertas con consulta rápida".
- `Program.cs` se fusionó automáticamente con los dos registros de servicios.

### Resolución 2 (`510727e`): main (con A y B) incorporado en C
Conflictos en `OperacionesController.cs`, `Program.cs` e `Incidencias.cshtml`.
- **Controlador:** el constructor recibe Algolia, la caché y `PieHostPublisher`. En `Cerrar` el orden queda **guardar en la base → invalidar Redis → publicar en PieHost**. Se conservan el listado cacheado y el endpoint `EstadoIncidencias`.
- **Program.cs:** se registran los tres servicios (Algolia, Redis y PieHost).
- **Vista:** se conservan el formulario de búsqueda y el indicador y cliente WebSocket. El título final es "Incidencias abiertas en tiempo real".

## Pruebas

### Locales (antes de cada PR y después de cada resolución)
- Login del supervisor y listado con 9 incidencias abiertas. Al cerrar una incidencia, el total baja a 8.
- Redis local: primera lectura desde la base (miss), luego lecturas desde Redis (hit) y `TTL incidencias:abiertas` = 60. Al cerrar, `EXISTS` = 0.
- Después de la resolución 2, los logs de un cierre muestran este orden (en local, sin credenciales de PieHost):
  ```
  Incidencia 5 cerrada en la base de datos
  Redis: clave incidencias:abiertas invalidada
  PieHost no configurado; no se publicó IncidenciaActualizada para la incidencia 5
  ```
  Con credenciales, la última línea es `PieHost: publicado IncidenciaActualizada {Id=5, Estado=Cerrada} en canal incidencias`.

### Resultados obtenidos en Render (producción)
| Prueba | Resultado |
|---|---|
| URL pública y login del supervisor | Responde HTTP 200 y el login funciona. Sin sesión, `/Operaciones/Incidencias` redirige al login. |
| Operador intentando cerrar | `AccessDenied` (solo el supervisor puede cerrar). |
| Algolia | "anclaje" → #6 y #1; "Parque Kennedy" → #7 y #2; "freno" → #3. |
| Algolia sin cerradas | Tras cerrar la #1, "anclaje" solo devuelve la #6. |
| Redis | Los logs alternan `BASE DE DATOS (miss…)` y `REDIS (hit…)`. La clave expira a los 60 s y se invalida al cerrar. |
| PieHost | Un cliente WebSocket conectado al canal recibió `IncidenciaActualizada` al cerrar la #1, sin recargar. |
| Claves | El HTML no contiene el App ID ni las claves de Algolia, ni el secreto de PieHost. |

### Cómo repetir las pruebas en Render
1. Entrar con `supervisor@incidencias.com`.
2. **Algolia:** buscar "anclaje" y comprobar que devuelve las incidencias de ese texto. Cerrar una y repetir la búsqueda: la cerrada ya no aparece.
3. **Redis:** recargar el listado general dos veces. En Render → *Logs* aparece `Listado leído desde REDIS (hit…)`.
4. **PieHost:** abrir la pantalla en dos sesiones (una normal y otra en incógnito). Cerrar una incidencia en la primera: en la segunda la fila desaparece sin recargar y aparece el aviso "cerrada (actualizado en tiempo real)".
5. **Commit:** el pie de página muestra `Commit desplegado: <sha>`, que coincide con el último commit de `main`.

## Ejecutar localmente

```bash
dotnet run --project src/Incidencias
```

Sin variables de entorno la app funciona igual: el listado sale siempre de la base, la búsqueda avisa que Algolia no está configurado y el WebSocket aparece como "no configurado".

> Nota: SQLite vive dentro del contenedor de Render. Con cada despliegue la base se recrea con los datos de prueba.
