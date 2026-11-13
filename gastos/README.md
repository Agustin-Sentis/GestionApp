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
| POST | `/api/auth/signin` | Login, devuelve JWT. Responde `{ "requiresTwoFactor": false, "token": …, "userId": …, "email": …, "expiresAtUtc": … }`. **Si el usuario activó la verificación en dos pasos**, la contraseña sola no es una sesión: responde `{ "requiresTwoFactor": true, "challengeToken": "…" }`, sin `token` (ver "Verificación en dos pasos") |
| POST | `/api/auth/2fa/verify` | `{ "challengeToken": "…", "code": "…" }`: termina el login con el código de 6 dígitos de la app o uno de recuperación y devuelve el JWT como `signin`. 400 `"Código incorrecto o vencido."` (siempre el mismo mensaje); **401** si el desafío venció (5 minutos), ya no corresponde (cambió la contraseña, se desactivó el 2FA) o no es un desafío; **429** si la cuenta está bloqueada, con `Retry-After` y `retryAfterSeconds` en el cuerpo |
| POST | `/api/auth/forgot-password` | `{ "email": "…" }`: si el correo tiene una cuenta, le manda un mail con un enlace para elegir una contraseña nueva. **Responde 204 siempre**, esté o no registrado (así no sirve para averiguar qué correos tienen cuenta), y no espera al mail. Máx. un mail por minuto por cuenta (el segundo no sale y se responde igual). 400 solo si el email no tiene formato válido. Mismo límite de intentos que el resto de `/api/auth/*` |
| POST | `/api/auth/reset-password` | `{ "token": "…", "newPassword": "…" }`: cambia la contraseña con el token del mail. 204; **no inicia sesión**. 400 si la contraseña incumple las reglas (el enlace **sigue sirviendo**) o si el enlace venció, ya se usó o no existe (un único mensaje: "El enlace venció o ya se usó.") |
| GET | `/api/category` | Categorías del usuario |
| POST | `/api/category` | Crear categoría |
| PUT | `/api/category/{id}/budget` | Fijar el presupuesto mensual de una categoría de gasto: `{ "monthlyBudget": 300000 }` (en ARS, mayor a cero, máx. 1.000.000.000), o `null` para quitarlo. 400 si la categoría es de ingreso; 404 si no es del usuario. Devuelve la categoría |
| DELETE | `/api/category/{id}` | Borrar (409 si tiene movimientos) |
| GET | `/api/transaction/paged?year=&month=&from=&to=&categoryId=&type=&search=&page=&pageSize=` | Listar paginado: `{ items, total, page, pageSize, totalPages }`. Mes (`year`+`month`) o rango (`from`+`to`), no ambos. `search` busca el texto dentro de la nota (sin distinguir mayúsculas, máx. 100 caracteres). `pageSize` por defecto 20, máx. 100 |
| GET | `/api/transaction/export?year=&month=&from=&to=&categoryId=&type=&search=` | Los mismos filtros (sin paginar) como archivo CSV: separador `;`, coma decimal y UTF-8 con BOM para que Excel en español lo abra bien. Máx. 10.000 filas: si hay más, 400 pidiendo acotar los filtros |
| GET | `/api/transaction/{id}` | Un movimiento |
| POST | `/api/transaction` | Crear |
| PUT | `/api/transaction/{id}` | Editar |
| DELETE | `/api/transaction/{id}` | Borrar |
| GET | `/api/report/monthly?year=&month=` | Resumen mensual en ARS. Cada movimiento en dólares se convierte con la cotización guardada con él, del dólar con el que se guardó (un mes puede mezclar dólares). Incluye `pendingConversions` (qué dólares necesitaron la cotización de hoy) y `previous`: ingresos, gastos y balance del mes anterior (con las mismas reglas), para compararlo. `previous` es `null` solo para enero de 2000. Incluye también `budgets`: una entrada por cada categoría de gasto con presupuesto (aunque ese mes no tenga gastos), con `monthlyBudget`, `spent` (lo gastado, en ARS, con las mismas reglas del reporte), `usedPercent` y `status` (`Ok`, `Warning` desde el 80% o más, `Exceeded` al pasar el 100%; gastar justo el 100% sigue siendo `Warning`), las más urgentes primero |
| GET | `/api/settings` | Ajustes del usuario: `{ "dollarType": "Official" \| "Blue" \| "Mep", "email": "…", "budgetEmailAlerts": false, "emailConfirmed": false, "twoFactorEnabled": false }`: el dólar con el que se guardan sus movimientos nuevos, su correo, el estado de los avisos por mail y si activó la verificación en dos pasos (el secreto y los códigos nunca viajan acá) |
| PUT | `/api/settings` | Cambiar el dólar elegido. Vale de ahí en adelante: los movimientos ya guardados conservan el suyo |
| PUT | `/api/settings/budget-alerts` | `{ "enabled": true \| false }`: prende o apaga los avisos de presupuesto por correo. Al prenderlos con el correo sin confirmar manda el mail de confirmación (máx. uno por minuto: 400 si se pide antes; 503 si no se pudo enviar, y entonces no se guarda nada). Devuelve los ajustes |
| POST | `/api/settings/resend-confirmation` | Manda otro mail de confirmación al correo de la cuenta (no hace nada si ya está confirmado). Máx. uno por minuto: 400 si se pide antes; 503 si no se pudo enviar. 204 |
| PUT | `/api/settings/email` | `{ "email": "…", "password": "…" }`: corrige el correo de una cuenta que **todavía no lo confirmó** (por ejemplo un error de tipeo al registrarse). Pide la contraseña actual y tiene el mismo límite de intentos que `/api/auth/*`. El correo nuevo queda sin confirmar y recibe el mail; los enlaces mandados al correo anterior (de confirmación y de recuperación) dejan de servir. 400 si la contraseña no es la correcta (nunca 401: la app cierra la sesión ante un 401) o el formato no es válido; 409 si el correo ya lo tiene otra cuenta o el actual ya está confirmado; 503 si no se pudo enviar el mail, y entonces no se cambia nada. Devuelve los ajustes |
| POST | `/api/settings/confirm-email` | `{ "token": "…" }`: confirma el correo con el token del enlace. **Sin sesión** (el enlace puede abrirse en otro dispositivo) y con el mismo límite de intentos que `/api/auth/*`. 204; 400 si el enlace venció, ya se usó o no existe |
| POST | `/api/settings/2fa/setup` | `{ "password": "…" }`: empieza a activar la verificación en dos pasos. Genera un secreto (se guarda **cifrado**, pero no vale hasta confirmar) y devuelve `{ "uri": "otpauth://totp/…", "secret": "…" }`: la `uri` es lo que dibuja el QR y `secret` es la misma clave como texto. Llamarlo de nuevo reemplaza el secreto de una activación a medias. 400 si la contraseña es incorrecta; 409 si ya está activada |
| POST | `/api/settings/2fa/enable` | `{ "code": "123456" }`: confirma con el primer código de la app, la activa y devuelve `{ "codes": [ … 10 códigos … ] }`: **la única vez que se muestran**. 400 si el código es incorrecto; 409 si ya está activada o no se llamó antes a `setup` |
| POST | `/api/settings/2fa/disable` | `{ "password": "…", "code": "…" }`: la desactiva y borra el secreto y los códigos. El código puede ser el de la app o uno de recuperación. 400 si la contraseña o el código son incorrectos; 409 si no está activada |
| POST | `/api/settings/2fa/recovery-codes` | `{ "password": "…", "code": "…" }`: reemplaza los códigos de recuperación (los anteriores dejan de servir) y devuelve los nuevos. Mismos errores que `disable` |

Errores: siempre `{ "error": "mensaje" }` con 400 (validación), 401, 404, 409, 429 (demasiados intentos en `/api/auth/*`, o cuenta bloqueada por códigos de la verificación en dos pasos incorrectos; en los dos casos con `Retry-After`, y en el segundo también `retryAfterSeconds` en el cuerpo), 503 (servicio externo caído) o 500 (sin detalle interno). Los endpoints de `/api/settings/2fa/*` tienen el mismo límite de intentos por IP que `/api/auth/*`, porque verifican una contraseña.

**Cotización del dólar.** Cada usuario elige con qué dólar se guardan sus movimientos nuevos (`/api/settings`: oficial, blue o MEP; por defecto la oficial). Cuando se guarda un movimiento en USD, queda con **ese dólar** (`DollarType` del movimiento) y con las cotizaciones de venta de dolarapi.com del día: la oficial (`ExchangeRate`, la columna que ya existía), la blue (`BlueExchangeRate`) y la MEP (`MepExchangeRate`; en dolarapi se llama `bolsa`). Elegir otro dólar después **no modifica ningún movimiento ya guardado**: cada uno se sigue leyendo con el suyo, así los meses cerrados no cambian de valor. `exchangeRate`, `arsAmount` y `dollarType` de cada movimiento son los de su propio dólar, y el CSV lo dice en una columna.

Al editar un movimiento, si cambia el monto, la fecha o la moneda se vuelven a tomar las cotizaciones de hoy y se guarda con el dólar que el usuario tenga elegido en ese momento; si solo cambia la nota o la categoría, conserva el suyo.

El reporte agrupa y suma en SQL, convirtiendo cada movimiento con su cotización (por eso un mes puede mezclar dólares). Si a un movimiento le falta la cotización de su dólar (no estaba disponible al cargar), queda pendiente y el reporte lo resuelve con la cotización actual de **ese** dólar (cache de 15 minutos), avisándolo en `pendingConversions`; si tampoco está disponible, ese monto queda afuera y la respuesta trae `hasUnconvertedUsd: true`. Si no hay USD no consulta la cotización. Los movimientos anteriores a esta función son oficiales, que es la cotización que guardaron: no se inventan cotizaciones históricas.

**Presupuestos.** Una categoría de gasto puede tener un presupuesto mensual en pesos (`Category.MonthlyBudget`, nulo = sin presupuesto). Es un único monto para todos los meses: cambiarlo también cambia cómo se mide un mes pasado (el panel lo aclara). Lo gastado se mide como el resto del reporte, con cada movimiento en dólares convertido con la cotización guardada con él. Si algún movimiento en USD quedó sin convertir (`hasUnconvertedUsd`), el estado se calcula con lo que hay y el panel lo avisa. Los umbrales (80% y 100%) viven solo en el backend (`ReportService`). Todavía no hay avisos por correo: están previstos como etapa siguiente.

**Avisos de presupuesto por correo.** Son opcionales: cada usuario los prende con una casilla en **Ajustes › Notificaciones** (el botón flotante de abajo a la izquierda; la tarjeta Presupuestos tiene un enlace ahí) (apagados por defecto) y tienen **doble confirmación**. Al prenderlos, la API manda un mail con un enlace (`/confirmar-correo?token=…`, válido 24 horas, de un solo uso; solo el último enlace pedido sirve). Los avisos empiezan cuando se abre. El token viaja en el mail y en la base queda únicamente su hash SHA-256 (`UserTokens`).

**Confirmar el correo al registrarse.** El registro manda el mismo mail de confirmación, en segundo plano (`IBackgroundRunner`): si no sale, la cuenta se crea igual. **No bloquea nada**: los usuarios que ya existían no tienen el correo confirmado, y bloquearlos los dejaría afuera. Mientras no esté confirmado, el encabezado muestra un aviso ("Correo sin confirmar") con "Reenviar el correo" y "¿Te equivocaste de correo?"; este último pide la contraseña y reemplaza el correo (solo mientras no esté confirmado), y deja sin efecto todo enlace mandado al anterior. Reenviar mantiene el minuto entre mails; cambiar el correo no, porque el mail del registro fue a la dirección equivocada. El aviso desaparece solo al volver a la pestaña después de abrir el enlace.

El aviso sale después de **guardar o editar un gasto**, en segundo plano: guardar nunca espera al correo ni falla por él. Se manda cuando la categoría del mes en curso (hora de Argentina) llega al 80% (`Warning`) o pasa el 100% (`Exceeded`), **una sola vez por categoría, mes y nivel**: la marca es una fila de `BudgetAlerts` con un índice único, así que dos gastos simultáneos que cruzan el mismo umbral mandan un solo mail. Un salto directo a superado manda solo el de superado. Si el envío falla, la marca se borra y el próximo gasto vuelve a intentarlo. Cargar gastos de meses anteriores, editar un presupuesto o borrar un movimiento no mandan nada.

**Recuperación de contraseña.** Desde la pantalla de acceso ("¿Olvidaste tu contraseña?") el usuario pone su correo y recibe un enlace a `/restablecer-contrasena?token=…`. El token es aleatorio, **dura 1 hora**, se usa **una sola vez**, solo vale el último pedido y en la base queda únicamente su hash SHA-256 (`UserTokens`, con el propósito `PasswordReset`; el de confirmar el correo es otro propósito y no sirve acá). Pedirlo responde siempre lo mismo, exista o no la cuenta, y el mail sale en segundo plano (`IBackgroundRunner`), así que ni la respuesta ni el tiempo delatan nada.

Al restablecer: se guarda la contraseña nueva (mismas reglas que el registro; si incumple, el enlace no se gasta), el correo queda **confirmado** (el enlace solo llegó a su dueño), se manda un mail de aviso "tu contraseña fue cambiada" (si ese mail falla, el cambio igual se hace) y **se cierran todas las sesiones abiertas**. Esto último funciona así: el JWT es sin estado, así que no se puede revocar; en cambio, `User.PasswordChangedAt` guarda el momento del cambio y cada JWT lleva su fecha de emisión (`iat`). En cada pedido autenticado, `UserSessionValidator` (enganchado en `OnTokenValidated` de JwtBearer) busca al usuario por clave primaria y rechaza con 401 la sesión si el usuario ya no existe o si `iat` es anterior al cambio (se compara en segundos, para no rechazar un inicio de sesión hecho justo después). Cuesta **una consulta por clave primaria por pedido autenticado**. Quien nunca restableció su contraseña no se ve afectado.

**Verificación en dos pasos.** Es **opcional** (`User.TwoFactorEnabled`, apagada por defecto: quien no la activa entra como siempre) y se maneja desde Ajustes › Seguridad. El segundo factor es una app autenticadora (Google Authenticator, Authy, 1Password…): el servidor no genera ni manda ningún código, solo guarda un secreto compartido (TOTP, RFC 6238, de 6 dígitos cada 30 segundos, con un paso de tolerancia hacia cada lado por si el reloj del celular está corrido) y valida lo que muestra la app. El QR lo dibuja el navegador a partir de la `uri` `otpauth://` de `setup`.

- **Login en dos pasos.** Con el 2FA activo, `signin` con la contraseña correcta no devuelve el JWT de sesión sino un **desafío**: un JWT de 5 minutos con el claim `purpose=2fa`, **firmado con una clave derivada** de `Jwt:Key` (`HMAC(Jwt:Key, "2fa-challenge")`). Por eso el desafío no sirve como sesión (el middleware de JWT no puede verificar su firma) ni una sesión como desafío; además `AuthService` exige el claim. `POST /api/auth/2fa/verify` lo canjea, junto con el código, por el JWT de sesión. No hay estado ni tablas para el desafío; deja de valer si cambió la contraseña (misma regla que las sesiones: su `iat` contra `PasswordChangedAt`) o si el 2FA se desactivó.
- **Qué guarda la base.** `Users.TwoFactorSecret` (el secreto **cifrado** con ASP.NET Data Protection; hay que poder leerlo para validar, así que no puede ser un hash), `TwoFactorEnabled`, `TwoFactorLastStep` (el último paso de 30 segundos aceptado: el mismo código no se puede usar dos veces), `TwoFactorFailedAttempts` y `TwoFactorLockedUntil`. Los códigos de recuperación están en `RecoveryCodes`, **solo con su hash BCrypt**.
- **Las claves de Data Protection viven en la base** (tabla `DataProtectionKeys`, `PersistKeysToDbContext`). Importa: los contenedores de Render y Docker pierden su disco al reiniciarse, y con claves en archivos cada reinicio dejaría todos los secretos ilegibles y a todos los usuarios con 2FA sin poder entrar. Una prueba de integración lo cubre. Si se rota o se borra esa tabla, los secretos dejan de poder descifrarse.
- **Códigos de recuperación.** Son 10, con formato `XXXXX-YYYYY` (sin caracteres que se confunden: 0/O, 1/I), de un solo uso, se aceptan en lugar del código de la app (en cualquier combinación de mayúsculas, guiones y espacios) y se muestran **una sola vez**, al activar o al generar nuevos. Generar nuevos reemplaza los anteriores en una sola transacción.
- **Bloqueo.** Después de **5 fallos seguidos** (un código incorrecto en `2fa/verify`, `enable`, `disable` o `recovery-codes`, o una contraseña incorrecta en las acciones de `/api/settings/2fa/*`) la cuenta queda bloqueada **15 minutos**: responde 429 aunque se mande el código correcto, y el bloqueo está en la base, así que sobrevive a un reinicio. Un acierto reinicia el contador. El quinto fallo todavía responde 400; el bloqueo se nota desde el siguiente intento.
- **Restablecer la contraseña no saltea el 2FA.** Solo cambia la contraseña y cierra las sesiones; el siguiente login sigue pidiendo el código. Por eso los códigos de recuperación son la única salida si se pierde el celular. Y activar, desactivar o regenerar piden **la contraseña y un código**: una sesión robada no alcanza para apagarlo.
- **Fuera de alcance a propósito:** "confiar en este dispositivo", códigos por SMS o mail, llaves de seguridad (WebAuthn), obligar a todos a activarlo y límite por IP propio (el límite de `/api/auth/*` ya aplica).

El envío está detrás de `IEmailSender`. Hoy hay dos implementaciones: `BrevoEmailSender` (API HTTPS de Brevo, con reintento, circuit breaker y timeout de Polly) y `NoOpEmailSender`, que se usa cuando faltan la clave o el remitente y solo registra el asunto en el log (sin cuerpo ni enlaces). Cambiar de proveedor (por ejemplo a Resend cuando haya dominio propio) es escribir otro adaptador y registrarlo en `Program.cs`.

| Variable | Qué hace |
|---|---|
| `Email__ApiKey` | Clave de API de Brevo. Se crea con acceso **Restricted** y solo **Transactional email: All**. Sin ella no se manda nada. |
| `Email__SenderEmail` | Remitente. Tiene que estar verificado en Brevo (*Senders, domains & dedicated IPs*). Sin dominio propio, un Gmail verificado sirve, pero esos mails pueden caer en spam. |
| `Email__SenderName` | Nombre del remitente (`DualBalance` por defecto). |
| `Email__AppBaseUrl` | URL pública del frontend, sin barra final: con ella se arman los enlaces de los mails (`http://localhost:5173` por defecto). **En producción tiene que ser la URL de Vercel.** |

En `docker compose` se cargan desde `BREVO_API_KEY`, `EMAIL_SENDER` y `FRONTEND_URL` del `.env`.

> **La clave de Brevo vence a los 90 días sin uso** (además de la fecha de vencimiento que se elige al crearla). En una app con poco tráfico los avisos pueden dejar de funcionar en silencio. Si dejan de llegar, mirá primero el log de la API: un 401 de Brevo (`Brevo refused the email …: 401`) significa clave vencida o revocada; se crea una nueva y se actualiza `Email__ApiKey`.

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

Variables que usa la API en contenedor: `ConnectionStrings__GastosConnectionStrings`, `Jwt__Key`, `Cors__AllowedOrigin` y, opcionales, las de correo (`Email__*`, ver "Avisos de presupuesto por correo").

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
| `Email__ApiKey`, `Email__SenderEmail`, `Email__AppBaseUrl` | la clave de Brevo, el remitente verificado y `https://gestionapp-frontend-delta.vercel.app` | Para que los avisos por correo funcionen (ver arriba). Sin la clave la API anda igual, pero no manda mails. |

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
