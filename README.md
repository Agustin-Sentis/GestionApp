# 💸 DualBalance · gestor de gastos

**DualBalance** es una aplicación web full-stack para registrar ingresos y gastos en **pesos o dólares**, organizarlos por categoría y ver un resumen mensual con gráficos. Los movimientos en USD se convierten a ARS con la cotización oficial del día en que se cargan, que queda guardada con cada movimiento: los meses cerrados no cambian de valor.

Proyecto de portfolio: backend en **.NET 10** con Clean Architecture y frontend en **React + TypeScript**, desplegado en producción.

| | |
|---|---|
| 🌐 **App** | https://gestionapp-frontend-delta.vercel.app |
| 📚 **API (Scalar)** | https://gestionapp-j0j4.onrender.com/scalar |

> La API corre en el plan gratuito de Render: si estuvo un rato sin uso, la primera petición puede tardar hasta un minuto en responder.

<!-- Agregá acá una captura o un GIF de la app:
![Dashboard](docs/dashboard.png)
-->

## ✨ Funcionalidades

- Registro e inicio de sesión con **JWT**, con límite de intentos por IP para frenar la adivinación de contraseñas.
- Categorías de gasto e ingreso por usuario (el nombre no distingue mayúsculas, y una categoría con movimientos no se puede borrar).
- Alta, edición y baja de movimientos, en **ARS o USD**, con confirmación antes de eliminar y avisos al guardar o fallar.
- Listado paginado con filtros por categoría, tipo, mes o rango de fechas y texto de la nota; los filtros viven en la URL, así que un enlace reproduce la misma vista.
- Exportación a **CSV** (abre en Excel) de lo que muestran los filtros.
- Resumen mensual: ingresos, gastos y balance, con cinco gráficos a elegir (destino de los ingresos, gastos por categoría, ingresos por categoría, ranking de categorías e ingresos vs. gastos). Cada uno trae su tabla de detalle con los montos y porcentajes, y cada categoría conserva su color en todos. La elección queda en la URL.
- Modo claro, oscuro o el del sistema, a elección del usuario.
- Página de inicio que explica qué hace la app, y un panel que entra completo en la pantalla: tres paneles lado a lado en pantallas grandes y pestañas en pantallas chicas.
- Conversión USD → ARS con la cotización de [dolarapi.com](https://dolarapi.com) del día en que se carga el movimiento, que se guarda con él. La consulta se cachea 15 minutos y se reintenta si el servicio falla; si sigue caído, el movimiento se guarda igual sin cotización y el reporte la resuelve después.
- Cada usuario ve y modifica únicamente sus propios datos.

## 🧱 Stack

**Backend**
- .NET 10, ASP.NET Core con **Controllers**
- Entity Framework Core 10 + PostgreSQL (Npgsql)
- FluentValidation, JWT Bearer, BCrypt
- Polly (retry y circuit breaker) sobre `IHttpClientFactory`
- OpenAPI + **Scalar** para probar los endpoints
- xUnit + Moq, y Testcontainers para probar el SQL contra un PostgreSQL real

**Frontend**
- React 19, TypeScript, Vite
- Tailwind CSS v4, con identidad propia: "el cuaderno" (hoja cuadriculada con margen rojo y tinta de birome; el modo oscuro es un pizarrón verde). Tipografías Archivo, Martian Mono y Shantell Sans, autoalojadas
- TanStack Query, React Router, Recharts (cargado bajo demanda)
- Vitest + Testing Library

**Infraestructura**
- Docker (multi-stage) y Docker Compose
- GitHub Actions (build y tests del backend, incluidos los de integración con PostgreSQL; lint, tests y build del frontend; construcción de las imágenes de Docker), con CodeQL y Dependabot para la seguridad y las dependencias
- API en Render, base de datos en Neon, frontend en Vercel

## 🏛️ Arquitectura

Clean Architecture en capas, con las dependencias apuntando hacia adentro:

```
gastos/src/
├── GastosBackend.Domain/          Entidades y enums
├── GastosBackend.Application/     Servicios, interfaces, DTOs, validaciones, mappers, excepciones
├── GastosBackend.Infrastructure/  EF Core, repositorios, autenticación, cliente de dolarapi, Polly
├── GastosBackend/                 Presentation: controllers, middleware, configuración
├── GastosBackend.Tests/           Tests unitarios (sin Docker)
└── GastosBackend.IntegrationTests/ Migraciones y consultas SQL contra PostgreSQL real (Testcontainers)
```

Patrones aplicados: Repository y Generic Repository, Service Layer, DTO + Mapper, Soft Delete, Adapter/Gateway, Options Pattern, Middleware de manejo de errores y Resilience (Polly).

El frontend, en `gastos/frontend/src/`:

```
├── pages/               Inicio (pública), acceso y panel
├── components/          Resumen, gráficos, libro de movimientos, formulario, categorías, tema, avisos
├── chartData.ts         Lógica de los gráficos, pura y testeada
├── useListFilters.ts    Filtros y paginación guardados en la URL
├── api.ts               Cliente HTTP tipado
└── index.css            Sistema de diseño: colores del tema, tipografías y piezas base
```

## 🔌 Endpoints principales

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/auth/signup` · `/api/auth/signin` | Registro e inicio de sesión |
| GET | `/api/category` | Categorías del usuario |
| POST | `/api/category` | Crear una categoría |
| DELETE | `/api/category/{id}` | Borrar una categoría (409 si tiene movimientos) |
| GET | `/api/transaction/paged` | Movimientos paginados; filtros: mes (`year`+`month`) o rango (`from`+`to`), `categoryId`, `type`, `search` (texto de la nota) |
| GET | `/api/transaction/export` | Los mismos filtros, como archivo CSV (hasta 10.000 filas) |
| GET | `/api/transaction/{id}` | Un movimiento |
| POST | `/api/transaction` | Crear un movimiento |
| PUT | `/api/transaction/{id}` | Editar un movimiento |
| DELETE | `/api/transaction/{id}` | Borrar un movimiento |
| GET | `/api/report/monthly?year=&month=` | Resumen mensual en ARS |
| GET | `/api/health` | Estado de la API y de la base de datos |

La documentación interactiva completa está en `/scalar`.

## 🛠️ Mejoras pendientes

Es un proyecto en evolución. Lo que tengo identificado para seguir:

**Diseño y experiencia de usuario**
- [x] Rediseño visual con identidad propia (tipografía, paleta, iconografía) y página de inicio.
- [x] Pantalla de edición de movimientos en el frontend (la API ya soporta `PUT /api/transaction/{id}`).
- [x] Modo oscuro con selector manual.
- [x] Estados de carga con *skeletons*.
- [ ] Mejorar la versión mobile: el panel ya pasa a pestañas en pantallas chicas, pero falta revisarlo y pulirlo en dispositivos reales.
- [x] Confirmación antes de eliminar y avisos (*toasts*) al guardar o fallar.
- [x] Dividir el bundle de JavaScript (*code splitting*): Recharts se carga bajo demanda y el paquete principal bajó de 664 kB a unos 340 kB.

**Funcionalidades**
- [x] Filtros en el listado por categoría, rango de fechas y texto de la nota.
- [ ] Presupuestos mensuales por categoría con alertas al superarlos.
- [ ] Movimientos recurrentes (alquiler, suscripciones).
- [x] Exportación a CSV.
- [ ] Reportes por rango de meses y comparación contra el mes anterior.
- [ ] Elegir el tipo de cotización del dólar (oficial, blue, MEP): hoy se define en la configuración del servidor, no lo elige el usuario.
- [x] Paginación del listado de movimientos.
- [ ] Recuperación de contraseña y refresh tokens.

**Calidad e infraestructura**
- [x] Tests de integración del backend con una base real (Testcontainers).
- [x] Tests del frontend (Vitest + Testing Library).
- [ ] Despliegue automático desde GitHub Actions y aplicación de migraciones en el pipeline.
- [ ] Monitoreo y logs estructurados.

## 👤 Autor

**Agustín Sentis** · Rosario, Argentina · [GitHub](https://github.com/Agustin-Sentis)
