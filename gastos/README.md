# DualBalance · gestor de gastos

DualBalance es una aplicación web para registrar ingresos y gastos (en ARS o USD), organizarlos por categoría y ver un resumen mensual. Backend en **.NET 10** (Controllers, Clean Architecture) y frontend en **React + TypeScript + Vite + Tailwind**.

La estructura y las convenciones del backend siguen las de VetCare.

## Estructura

```
gastos/
├── src/
│   ├── GastosBackend.slnx
│   ├── GastosBackend.Domain/          Entidades y enums (sin lógica, sin excepciones)
│   ├── GastosBackend.Application/     Interfaces, servicios, DTOs, validaciones, mappers, excepciones, options
│   ├── GastosBackend.Infrastructure/  EF Core + PostgreSQL, repositorios, AuthService, DolarApiService, Polly
│   ├── GastosBackend/                 Presentation: Program.cs, controllers, middleware, OpenApi/Scalar
│   ├── GastosBackend.Tests/           xUnit + Moq (servicios, validadores, controllers), sin Docker
│   └── GastosBackend.IntegrationTests/ xUnit + Testcontainers: migraciones y SQL contra PostgreSQL real
├── frontend/                          React 19 + Vite + Tailwind v4 + TanStack Query
├── docs/diagrama.mmd                  Diagrama de clases (Mermaid)
└── docker-compose.yml
```

El workflow de CI no está en esta carpeta: la raíz del repositorio es la carpeta de arriba (`GestionApp/`) y GitHub solo ejecuta los workflows de `<raíz>/.github/workflows/ci.yml`.

Las dependencias apuntan hacia adentro: `Presentation → Application + Infrastructure`, `Infrastructure → Application → Domain`.

## Patrones usados

| Patrón | Dónde |
|---|---|
| Repository + Generic Repository | `IBaseRepository<T>` / `BaseRepository<T>` (open generic) y repos específicos |
| Service Layer | `Application/Services` |
| DTO + Mapper | `Application/dtos`, `Application/Mapper` (extension methods estáticos) |
| Soft Delete | `BaseEntity.IsDeleted` / `DeleteDate`, índices únicos filtrados |
| Adapter / Gateway | `IDolarApiService` → `DolarApiService` |
| Factory | `IHttpClientFactory` (cliente nombrado `dolarHttpClient`) |
| Resilience | Polly: retry + circuit breaker |
| Options | `JwtOptions`, `DolarApiOptions` |
| Middleware | `ExceptionHandlingMiddleware` |
| Validación | FluentValidation, errores como `ValidationException` |

No se usó Policy-based Authorization ni TPC: con un solo rol y pocas entidades no aportan.

## Correr en local

Requisitos: .NET 10 SDK, Node 22+, Docker.

```bash
cp .env.example .env            # completá DB_PASSWORD y JWT_KEY
docker compose up -d db         # PostgreSQL 17 en 127.0.0.1:5432

cd src
dotnet tool restore
dotnet user-secrets set "Jwt:Key" "<al-menos-32-caracteres>" --project GastosBackend

dotnet run --project GastosBackend
```

Las migraciones ya están en el repositorio (`src/GastosBackend.Infrastructure/Migrations`) y la API las aplica sola al iniciar. Solo hace falta generar una nueva cuando cambiás una entidad o el `DbContext`:

```bash
dotnet ef migrations add NombreDelCambio \
  --project GastosBackend.Infrastructure \
  --startup-project GastosBackend \
  --output-dir Migrations
```

La contraseña de `appsettings.Development.json` (`cambiame`) tiene que coincidir con `DB_PASSWORD` del `.env`.

El host de la base en desarrollo es `127.0.0.1` (no `localhost`) a propósito: `localhost` prueba primero IPv6 y, después de reiniciar el contenedor de la base, la primera conexión tarda varios segundos en caer a IPv4.

`GET /api/health` responde `200` si la API llega a la base y `503` si no. El tiempo máximo de espera a la base es `Health__DatabaseTimeoutSeconds` (8 por defecto).

Al iniciar, la API aplica las migraciones pendientes sola (`DatabaseMigrationService`). Si no existe ninguna migración, falla con un mensaje claro.

**Las migraciones solo agregan.** En un despliegue, la instancia vieja sigue atendiendo mientras la nueva migra, así que cada migración tiene que ser compatible con la versión anterior de la API: agregar tablas o columnas (nulas o con valor por defecto) está bien; borrar o renombrar algo que la versión anterior usa, no. Para eso se hace en dos pasos, en dos despliegues: primero la API deja de usar la columna, y recién después una migración la borra.

- API: http://localhost:8080
- **Scalar** (probar endpoints, solo en desarrollo): http://localhost:8080/scalar
- Health: `GET /api/health`

Frontend:

```bash
cd frontend
cp .env.example .env            # VITE_API_URL=http://localhost:8080
npm install
npm run dev                     # http://localhost:5173
```

Sin sesión, `/` muestra la página de inicio; **Crear cuenta** lleva al registro, y con la sesión iniciada la misma dirección muestra el panel.

### Probar en Scalar

1. `POST /api/auth/signup` con `{ "email": "demo@demo.com", "password": "Demo1234!" }`.
2. Copiá el `token` de la respuesta, tocá **Authentication → Bearer** y pegalo.
3. Ya podés usar el resto de los endpoints.

La contraseña exige mínimo 8 caracteres, mayúscula, minúscula, número y carácter especial.

## Endpoints

Los enums viajan como texto: `type` = `"Expense"` | `"Income"`, `currency` = `"ARS"` | `"USD"`.

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/auth/signup` | Registro, devuelve JWT |
| POST | `/api/auth/signin` | Login, devuelve JWT |
| GET | `/api/category` | Categorías del usuario |
| POST | `/api/category` | Crear categoría |
| DELETE | `/api/category/{id}` | Borrar (409 si tiene movimientos) |
| GET | `/api/transaction/paged?year=&month=&from=&to=&categoryId=&type=&search=&page=&pageSize=` | Listar paginado: `{ items, total, page, pageSize, totalPages }`. Mes (`year`+`month`) o rango (`from`+`to`), no ambos. `search` busca el texto dentro de la nota (sin distinguir mayúsculas, máx. 100 caracteres). `pageSize` por defecto 20, máx. 100 |
| GET | `/api/transaction/export?year=&month=&from=&to=&categoryId=&type=&search=` | Los mismos filtros (sin paginar) como archivo CSV: separador `;`, coma decimal y UTF-8 con BOM para que Excel en español lo abra bien. Máx. 10.000 filas: si hay más, 400 pidiendo acotar los filtros |
| GET | `/api/transaction/{id}` | Un movimiento |
| POST | `/api/transaction` | Crear |
| PUT | `/api/transaction/{id}` | Editar |
| DELETE | `/api/transaction/{id}` | Borrar |
| GET | `/api/report/monthly?year=&month=` | Resumen mensual en ARS |

Errores: siempre `{ "error": "mensaje" }` con 400 (validación), 401, 404, 409, 429 (demasiados intentos en `/api/auth/*`, con `Retry-After`), 503 (servicio externo caído) o 500 (sin detalle interno).

Cada movimiento en USD guarda la cotización de dolarapi.com vigente al cargarlo (`exchangeRate`, y `arsAmount` con el monto en pesos), así los meses cerrados no cambian de valor. El reporte agrupa y suma en SQL con esas cotizaciones. Si dolarapi estaba caído al cargar, el movimiento queda sin cotización y el reporte la resuelve con la actual (cache de 15 minutos); si tampoco está disponible, ese monto queda afuera y la respuesta trae `hasUnconvertedUsd: true`. Si no hay USD no consulta la cotización.

## Tests

### Frontend

[Vitest](https://vitest.dev/) + [Testing Library](https://testing-library.com/) sobre jsdom. No necesitan backend ni Docker: la API se simula reemplazando `fetch` (`src/test/fetchMock.ts`), y `renderWithProviders` (`src/test/render.tsx`) arma los mismos proveedores que la app.

```bash
cd frontend
npm test              # una pasada (es lo que corre el CI)
npm run test:watch    # se vuelve a correr al guardar
```

Los tests viven junto al código (`Algo.tsx` → `Algo.test.tsx`). Dos detalles para escribirlos:

- Con temporizadores falsos (`vi.useFakeTimers`) usá `fireEvent` y falseá solo `setTimeout`/`clearTimeout`. Con `userEvent`, Testing Library espera un `setTimeout` real y el test se cuelga; y si se falsean todos los temporizadores, React no llega a renderizar.
- Los tests comprueban comportamiento, no aspecto: cómo se ve algo hay que mirarlo en el navegador.

### Backend

Hay dos proyectos de tests:

| Proyecto | Qué prueba | Requiere Docker |
|---|---|---|
| `GastosBackend.Tests` | Servicios, validadores, controllers y la API con mocks. Rápido (unos segundos). | No |
| `GastosBackend.IntegrationTests` | Migraciones, índices y consultas SQL contra un **PostgreSQL real** que [Testcontainers](https://dotnet.testcontainers.org/) levanta en un contenedor descartable. | **Sí** |

```bash
dotnet test src/GastosBackend.Tests              # los rápidos, sin Docker
dotnet test src/GastosBackend.IntegrationTests   # contra PostgreSQL (Docker en marcha)
dotnet test src/GastosBackend.slnx               # todo (requiere Docker)
```

Los de integración usan su propio contenedor y bases con nombre único: **no tocan** la base de desarrollo del `docker-compose.yml`. La primera corrida descarga la imagen `postgres:17` y una imagen auxiliar de Testcontainers, así que necesita internet. En el CI son el job `integration`.

Si cambiás una entidad o el `DbContext` y te olvidás de `dotnet ef migrations add`, `SchemaTests.TheModel_HasNoChangesMissingFromTheMigrations` lo detecta.

## Docker

```bash
docker compose up -d --build              # db + api
docker compose --profile web up -d --build  # + frontend en :5173
```

Variables que usa la API en contenedor: `ConnectionStrings__GastosConnectionStrings`, `Jwt__Key`, `Cors__AllowedOrigin`.

> La API en Docker aplica las migraciones que van dentro de la imagen. Si agregás una (`dotnet ef migrations add`), reconstruí con `docker compose up -d --build`. En el despliegue, además, tiene que estar commiteada: la plataforma construye la imagen desde el repositorio.

## Deploy

Cualquier host que corra contenedores sirve (Fly.io, Railway, Render, un VPS). Necesitás una base PostgreSQL y definir `ConnectionStrings__GastosConnectionStrings`, `Jwt__Key` (32+ caracteres) y `Cors__AllowedOrigin` con la URL del frontend. El frontend se compila con `VITE_API_URL` apuntando a la API pública.

`frontend/vercel.json` redirige cualquier ruta que no sea un archivo a `index.html` (React Router: `/login` tiene que funcionar al refrescar) y agrega cabeceras de seguridad básicas.

### Variables de entorno en producción (Render)

Además de las tres obligatorias de arriba, estas son las que endurecen la API. Todas tienen un valor por defecto seguro para desarrollo, así que **en producción hay que definirlas a mano**:

| Variable | Valor en producción | Qué hace |
|---|---|---|
| `Cors__AllowedOrigin` | `https://gestionapp-frontend-delta.vercel.app` | Único origen que puede llamar a la API desde un navegador. Sin barra final. |
| `ForwardedHeaders__Enabled` | `true` | La API toma la IP y el esquema reales de `X-Forwarded-*`. Sin esto, el rate limiting cuenta todo como una sola IP y HSTS nunca se envía. |
| `Https__Hsts__Enabled` | `true` | Envía `Strict-Transport-Security`. Requiere `ForwardedHeaders__Enabled=true`. |
| `AllowedHosts` | `gestionapp-j0j4.onrender.com` | Dominio de la API, **solo el host** (sin `https://`; varios se separan con `;`). Cualquier otro `Host` recibe 400. |

Opcionales: `Https__Hsts__MaxAgeDays` (30 por defecto; subilo cuando HTTPS esté probado), `RateLimiting__Auth__PermitLimit` y `RateLimiting__Auth__WindowSeconds` (10 pedidos por 60 s por IP en `/api/auth/*`), `Health__DatabaseTimeoutSeconds` (8) y `Docs__Enabled`.

Detalles que conviene saber:

- **`ForwardedHeaders__Enabled`** está apagado por defecto porque solo es seguro cuando **todo** el tráfico pasa por el proxy; si no, un cliente podría falsear su IP.
- **`AllowedHosts`** vale `*` por defecto y la API avisa en el log al arrancar si sigue así fuera de desarrollo. Ojo: es el dominio de la **API**, no el del frontend. Si agregás un dominio propio, sumalo a la lista. Si el chequeo de salud de la plataforma usa otro `Host` y empieza a fallar, quitá la variable para volver a `*`.
- **HSTS**: una vez que un navegador lo recibe, no puede volver a HTTP con ese dominio hasta que vence (`MaxAgeDays`).
- **Scalar y OpenAPI** (`/scalar`, `/openapi/v1.json`) están desactivados fuera de desarrollo porque listan todos los endpoints. Se activan con `Docs__Enabled=true`; el `docker-compose.yml` local ya lo hace.

## CI

GitHub Actions (`GestionApp/.github/workflows/ci.yml`, en la raíz del repositorio; los comandos corren desde `gastos/`):

| Trabajo | Qué hace |
|---|---|
| `backend` | restore, build y tests rápidos (sin Docker) |
| `integration` | tests contra PostgreSQL real, levantado con Testcontainers |
| `frontend` | install, lint, tests y build |
| `docker` | construye las imágenes de la API y del frontend sin publicarlas: si un cambio rompe un Dockerfile, se ve acá y no al desplegar |
| `ci-ok` | resume los cuatro en un solo chequeo. Es el que conviene exigir en las reglas de la rama `main` |

Además hay dos workflows aparte: `codeql.yml` (análisis estático de seguridad, con los resultados en la pestaña Security) y `dependabot.yml` (PRs semanales de actualización de dependencias, agrupados; los paquetes de Microsoft y EF Core se actualizan juntos para que no vuelvan a quedar en versiones mezcladas). El SDK de .NET se fija en `gastos/global.json`, así que el CI y tu máquina compilan con la misma versión.

Los resultados de los tests (`.trx`) quedan como artefacto de cada corrida, por si hace falta ver el detalle de un fallo.
