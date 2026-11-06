# 💸 Gestor de gastos

Aplicación web full-stack para registrar ingresos y gastos en **pesos o dólares**, organizarlos por categoría y ver un resumen mensual con gráfico. Los movimientos en USD se convierten a ARS con la cotización oficial en tiempo real.

Proyecto de portfolio: backend en **.NET 10** con Clean Architecture y frontend en **React + TypeScript**, desplegado y siempre disponible.

| | |
|---|---|
| 🌐 **App** | https://gestionapp-frontend-delta.vercel.app |
| 📚 **API (Scalar)** | https://gestionapp-j0j4.onrender.com/scalar |

> La API corre en el plan gratuito de Render: si estuvo un rato sin uso, la primera petición puede tardar hasta un minuto en responder.

<!-- Agregá acá una captura o un GIF de la app:
![Dashboard](docs/dashboard.png)
-->

## ✨ Funcionalidades

- Registro e inicio de sesión con **JWT**.
- Categorías de gasto e ingreso por usuario.
- Alta, edición y baja de movimientos, en **ARS o USD**.
- Resumen mensual: ingresos, gastos, balance y gráfico por categoría.
- Conversión USD → ARS con la cotización de [dolarapi.com](https://dolarapi.com), cacheada 15 minutos y con reintentos si el servicio falla.
- Cada usuario ve y modifica únicamente sus propios datos.

## 🧱 Stack

**Backend**
- .NET 10, ASP.NET Core con **Controllers**
- Entity Framework Core 10 + PostgreSQL (Npgsql)
- FluentValidation, JWT Bearer, BCrypt
- Polly (retry y circuit breaker) sobre `IHttpClientFactory`
- OpenAPI + **Scalar** para probar los endpoints
- xUnit + Moq

**Frontend**
- React 19, TypeScript, Vite
- Tailwind CSS v4
- TanStack Query, React Router, Recharts

**Infraestructura**
- Docker (multi-stage) y Docker Compose
- GitHub Actions (build y tests del backend; lint y build del frontend)
- API en Render, base de datos en Neon, frontend en Vercel

## 🏛️ Arquitectura

Clean Architecture en capas, con las dependencias apuntando hacia adentro:

```
gastos/src/
├── GastosBackend.Domain/          Entidades y enums
├── GastosBackend.Application/     Servicios, interfaces, DTOs, validaciones, mappers, excepciones
├── GastosBackend.Infrastructure/  EF Core, repositorios, autenticación, cliente de dolarapi, Polly
├── GastosBackend/                 Presentation: controllers, middleware, configuración
└── GastosBackend.Tests/           Tests unitarios
```

Patrones aplicados: Repository y Generic Repository, Service Layer, DTO + Mapper, Soft Delete, Adapter/Gateway, Options Pattern, Middleware de manejo de errores y Resilience (Polly).

## 🔌 Endpoints principales

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/auth/signup` · `/api/auth/signin` | Registro e inicio de sesión |
| GET · POST · DELETE | `/api/category` | Categorías del usuario |
| GET · POST · PUT · DELETE | `/api/transaction` | Movimientos |
| GET | `/api/report/monthly?year=&month=` | Resumen mensual en ARS |

La documentación interactiva completa está en `/scalar`.

## 🚀 Correrlo en local

Requisitos: .NET 10 SDK, Node 22+ y Docker.

```bash
cd gastos
cp .env.example .env            # completá DB_PASSWORD y JWT_KEY
docker compose up -d db         # PostgreSQL

cd src
dotnet user-secrets set "Jwt:Key" "<clave-de-32+-caracteres>" --project GastosBackend
dotnet run --project GastosBackend      # http://localhost:8080/scalar
```

```bash
cd gastos/frontend
cp .env.example .env            # VITE_API_URL=http://localhost:8080
npm install
npm run dev                     # http://localhost:5173
```

Tests del backend: `dotnet test gastos/src/GastosBackend.slnx`

Más detalle (migraciones, Docker, deploy) en [`gastos/README.md`](gastos/README.md).

## 🛠️ Mejoras pendientes

Es un proyecto en evolución. Lo que tengo identificado para seguir:

**Diseño y experiencia de usuario**
- [ ] Rediseño visual: hoy es una interfaz funcional y simple; falta una identidad propia (tipografía, paleta, iconografía).
- [ ] Pantalla de edición de movimientos en el frontend (la API ya soporta `PUT /api/transaction/{id}`).
- [ ] Modo oscuro con selector manual.
- [ ] Mejorar la versión mobile y agregar estados de carga con *skeletons*.
- [ ] Confirmación antes de eliminar y avisos (*toasts*) al guardar o fallar.
- [ ] Dividir el bundle de JavaScript (*code splitting*): hoy supera los 500 kB por Recharts.

**Funcionalidades**
- [ ] Filtros en el listado por categoría, rango de fechas y texto de la nota.
- [ ] Presupuestos mensuales por categoría con alertas al superarlos.
- [ ] Movimientos recurrentes (alquiler, suscripciones).
- [ ] Exportación a CSV.
- [ ] Reportes por rango de meses y comparación contra el mes anterior.
- [ ] Elegir el tipo de cotización del dólar (oficial, blue, MEP).
- [ ] Paginación del listado de movimientos.
- [ ] Recuperación de contraseña y refresh tokens.

**Calidad e infraestructura**
- [ ] Tests de integración del backend con una base real (Testcontainers).
- [ ] Tests del frontend (Vitest + Testing Library).
- [ ] Despliegue automático desde GitHub Actions y aplicación de migraciones en el pipeline.
- [ ] Monitoreo y logs estructurados.

## 👤 Autor

**Agustín Sentis** · Rosario, Argentina · [GitHub](https://github.com/Agustin-Sentis)
