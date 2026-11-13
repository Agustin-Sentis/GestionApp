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
- Resumen mensual: ingresos, gastos y lo que te queda, cada uno comparado con el mes anterior ("▲ 25% vs. octubre"), con cinco gráficos a elegir (destino de los ingresos, gastos por categoría, ingresos por categoría, ranking de categorías e ingresos vs. gastos). Cada uno trae su tabla de detalle con los montos y porcentajes, y cada categoría conserva su color en todos. La elección queda en la URL.
- **Presupuestos mensuales** por categoría de gasto, en pesos: una barra de avance por categoría en el resumen, con el estado también en palabras ("Cerca del límite" desde el 80%, "Superado por $ X" al pasarte). El monto se edita ahí mismo.
- **Avisos por correo** (opcionales): un mail cuando una categoría llega al 80% del presupuesto y otro cuando lo supera, una sola vez por mes y nivel. Se prenden con una casilla en **Ajustes › Notificaciones** y se activan al confirmar el correo con un enlace.
- Modo claro, oscuro o el del sistema, a elección del usuario.
- Página de inicio que explica qué hace la app, y un panel que entra completo en la pantalla: tres paneles lado a lado en pantallas grandes y pestañas en pantallas chicas.
- Conversión USD → ARS con el dólar que elija el usuario: **oficial, blue o MEP**, con las cotizaciones de [dolarapi.com](https://dolarapi.com). La elección vale para los movimientos que se cargan de ahí en adelante: cada movimiento queda con el dólar y la cotización del día en que se guardó, y elegir otro después no lo cambia (los meses cerrados tampoco). Cada movimiento dice al lado con qué cotización se convirtió, y el formulario avisa con cuál se va a guardar. La consulta se cachea 15 minutos y se reintenta si el servicio falla; si no hay cotización, el movimiento se guarda igual y el resumen avisa cuál faltó.
- **Confirmación del correo** al registrarse: llega un mail con un enlace y, mientras no se abra, un aviso en el encabezado (sin bloquear nada) permite reenviarlo o corregir el correo si había un error de tipeo.
- **Recuperación de contraseña** por mail, con un enlace que dura 1 hora y sirve una sola vez. La pantalla responde igual aunque el correo no exista, y al restablecerla se cierran todas las sesiones que estaban abiertas.
- **Verificación en dos pasos** (opcional): además de la contraseña, al entrar se pide el código de 6 dígitos de una app autenticadora (Google Authenticator, Authy, 1Password…). Se activa escaneando un QR, y trae 10 códigos de recuperación de un solo uso por si se pierde el celular. Tras 5 códigos incorrectos seguidos se bloquea 15 minutos, y restablecer la contraseña por mail no la saltea.
- **Ajustes**: un botón flotante abre una ventana con la seguridad (la verificación en dos pasos) y las notificaciones (el estado del correo y los avisos de presupuesto).
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
| POST | `/api/auth/signup` · `/api/auth/signin` | Registro e inicio de sesión. Con la verificación en dos pasos activada, `signin` no devuelve la sesión sino un desafío (`requiresTwoFactor` y `challengeToken`, de 5 minutos) |
| POST | `/api/auth/2fa/verify` | Termina el inicio de sesión con el desafío y el código de la app (o uno de recuperación). 429 tras 5 códigos incorrectos seguidos |
| POST | `/api/auth/forgot-password` · `/api/auth/reset-password` | Recuperación de contraseña: pedir el enlace por mail (responde siempre lo mismo) y elegir la contraseña nueva con el token |
| GET | `/api/category` | Categorías del usuario |
| POST | `/api/category` | Crear una categoría |
| PUT | `/api/category/{id}/budget` | Fijar (o quitar, con `null`) el presupuesto mensual de una categoría de gasto |
| DELETE | `/api/category/{id}` | Borrar una categoría (409 si tiene movimientos) |
| GET | `/api/transaction/paged` | Movimientos paginados; filtros: mes (`year`+`month`) o rango (`from`+`to`), `categoryId`, `type`, `search` (texto de la nota) |
| GET | `/api/transaction/export` | Los mismos filtros, como archivo CSV (hasta 10.000 filas) |
| GET | `/api/transaction/{id}` | Un movimiento |
| POST | `/api/transaction` | Crear un movimiento |
| PUT | `/api/transaction/{id}` | Editar un movimiento |
| DELETE | `/api/transaction/{id}` | Borrar un movimiento |
| GET | `/api/report/monthly?year=&month=` | Resumen mensual en ARS, con los totales del mes anterior (`previous`) para compararlo y el avance de cada presupuesto (`budgets`) |
| GET | `/api/settings` | Ajustes del usuario: con qué dólar (`Official`, `Blue` o `Mep`) se guardan sus movimientos nuevos |
| PUT | `/api/settings` | Cambiar ese ajuste. Vale de ahí en adelante: los movimientos ya guardados conservan el suyo |
| PUT | `/api/settings/budget-alerts` | Prender o apagar los avisos de presupuesto por correo (al prenderlos manda un mail de confirmación) |
| POST | `/api/settings/resend-confirmation` | Reenviar el mail de confirmación del correo (máx. uno por minuto) |
| PUT | `/api/settings/email` | Corregir el correo mientras no esté confirmado (pide la contraseña; manda el mail de confirmación al correo nuevo) |
| POST | `/api/settings/confirm-email` | Confirmar el correo con el token del enlace del mail (sin sesión) |
| POST | `/api/settings/2fa/setup` · `enable` · `disable` · `recovery-codes` | Verificación en dos pasos: empezar la activación (QR), confirmarla con el primer código y recibir los códigos de recuperación, desactivarla, o generar códigos nuevos. Piden la contraseña y/o un código |
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
- [x] Presupuestos mensuales por categoría, con barra de avance y aviso dentro de la app al llegar al 80% y al superar el 100%.
- [x] Avisos de presupuesto por correo, a elección del usuario (apagados por defecto, con doble confirmación). Usa Brevo; el envío está detrás de una interfaz para cambiar de proveedor (por ejemplo a Resend cuando haya dominio propio).
- [ ] Movimientos recurrentes (alquiler, suscripciones).
- [x] Exportación a CSV.
- [x] Comparación contra el mes anterior: cada cifra del resumen muestra cuánto subió o bajó.
- [ ] Reportes por rango de meses (varios meses juntos, con su evolución).
- [x] Elegir el tipo de cotización del dólar (oficial, blue, MEP), por usuario: cada movimiento conserva el que estaba elegido al guardarlo y lo muestra a la vista.
- [x] Paginación del listado de movimientos.
- [x] Recuperación de contraseña por mail (enlace de 1 hora y de un solo uso). Al restablecerla se cierran todas las sesiones abiertas.
- [x] Confirmar el correo al registrarse: mail de confirmación al crear la cuenta, aviso en el encabezado mientras falte (sin bloquear el acceso) y forma de reenviarlo o corregir el correo si hubo un error de tipeo.
- [x] Verificación en dos pasos (TOTP con Google Authenticator y códigos de recuperación), opcional, con bloqueo temporal tras 5 códigos incorrectos. Las claves que cifran los secretos viven en la base de datos.
- [ ] Refresh tokens.

**Calidad e infraestructura**
- [x] Tests de integración del backend con una base real (Testcontainers).
- [x] Tests del frontend (Vitest + Testing Library).
- [ ] Despliegue automático desde GitHub Actions y aplicación de migraciones en el pipeline.
- [ ] Monitoreo y logs estructurados.

## 👤 Autor

**Agustín Sentis** · Rosario, Argentina · [GitHub](https://github.com/Agustin-Sentis)
