# Gestor de gastos

Aplicación web para registrar ingresos y gastos (en ARS o USD), organizarlos por categoría y ver un resumen mensual. Backend en **.NET 10** (Controllers, Clean Architecture) y frontend en **React + TypeScript + Vite + Tailwind**.

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
│   └── GastosBackend.Tests/           xUnit + Moq (servicios, validadores, controllers)
├── frontend/                          React 19 + Vite + Tailwind v4 + TanStack Query
├── docs/diagrama.mmd                  Diagrama de clases (Mermaid)
├── docker-compose.yml
└── .github/workflows/ci.yml
```

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

# Solo la primera vez: generar la migración inicial
dotnet ef migrations add Init \
  --project GastosBackend.Infrastructure \
  --startup-project GastosBackend \
  --output-dir Migrations

dotnet run --project GastosBackend
```

La contraseña de `appsettings.Development.json` (`cambiame`) tiene que coincidir con `DB_PASSWORD` del `.env`.

Al iniciar, la API aplica las migraciones sola (`DatabaseMigrationService`). Si no existe ninguna migración, falla con un mensaje claro.

- API: http://localhost:8080
- **Scalar** (probar endpoints): http://localhost:8080/scalar
- Health: `GET /api/health`

Frontend:

```bash
cd frontend
cp .env.example .env            # VITE_API_URL=http://localhost:8080
npm install
npm run dev                     # http://localhost:5173
```

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
| GET | `/api/transaction?year=&month=&categoryId=` | Listar (máx. 200) |
| POST | `/api/transaction` | Crear |
| PUT | `/api/transaction/{id}` | Editar |
| DELETE | `/api/transaction/{id}` | Borrar |
| GET | `/api/report/monthly?year=&month=` | Resumen mensual en ARS |

Errores: siempre `{ "error": "mensaje" }` con 400 (validación), 401, 404, 409, 503 (servicio externo caído) o 500 (sin detalle interno).

El reporte agrupa y suma en SQL; si hay movimientos en USD, convierte a ARS con la cotización de dolarapi.com (cache de 15 minutos). Si no hay USD no consulta la cotización.

## Tests

```bash
dotnet test src/GastosBackend.slnx
```

## Docker

```bash
docker compose up -d --build              # db + api
docker compose --profile web up -d --build  # + frontend en :5173
```

Variables que usa la API en contenedor: `ConnectionStrings__GastosConnectionStrings`, `Jwt__Key`, `Cors__AllowedOrigin`.

> Antes de levantar la API en Docker tiene que existir la carpeta `src/GastosBackend.Infrastructure/Migrations` (paso `dotnet ef migrations add Init`) y estar commiteada.

## Deploy

Cualquier host que corra contenedores sirve (Fly.io, Railway, Render, un VPS). Necesitás una base PostgreSQL y definir `ConnectionStrings__GastosConnectionStrings`, `Jwt__Key` (32+ caracteres) y `Cors__AllowedOrigin` con la URL del frontend. El frontend se compila con `VITE_API_URL` apuntando a la API pública.

## CI

GitHub Actions (`.github/workflows/ci.yml`): restore, build y test del backend; install, lint y build del frontend.
