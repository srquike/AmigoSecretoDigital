# Amigo Secreto Digital

Aplicación familiar para organizar el intercambio de Amigo Secreto. La solución usa .NET 10, Blazor Web App con interactividad en servidor y Supabase.

## Estructura

- `src/AmigoSecretoDigital.Web`: aplicación ASP.NET Core, componentes Blazor Interactive Server e integración Supabase.
- `src/AmigoSecretoDigital.Application`: reglas de aplicación, contrato `IExchangeService` y catálogo estático de frases.
- `src/AmigoSecretoDigital.Core`: modelo y estados del dominio.
- `supabase/migrations`: esquema SQL, políticas RLS y funciones de base de datos.

Los componentes interactivos y los servicios de confianza se ejecutan en ASP.NET Core. `IExchangeService` separa las pantallas del almacenamiento; con Supabase configurado se usa el cliente oficial .NET y, en desarrollo sin credenciales, un adaptador en memoria para la demo.

## Flujo acordado

1. El organizador crea un intercambio con nombre, fecha, condiciones y presupuesto opcional. Se genera un enlace único para ese intercambio.
2. El organizador comparte el mismo enlace con toda la familia; cada invitado lo usa para registrarse con nombre o apodo, edad, sexo y su propia frase privada.
3. El organizador puede cerrar la inscripción desde que haya tres participantes. La cantidad prevista es una meta; no hace falta alcanzarla. El límite técnico es de 100 participantes.
4. Una función confiable del lado de Supabase valida las restricciones y realiza el sorteo.
5. Administración muestra la lista de participantes con su amigo asignado y permite generar un enlace de revelación individual para cada participante.
6. La vista de revelación está separada de administración. El invitado abre su enlace personal e ingresa su frase privada para consultar su resultado. El catálogo de frases contiene 50 frases estáticas e independientes de los datos personales.

## Seguridad

- Los componentes del servidor mantienen las credenciales privadas fuera del navegador. Nunca incluir claves secretas de Supabase, contraseñas administrativas ni secretos de firma en componentes del cliente o archivos estáticos.
- La administración requiere una cuenta de Supabase Auth con correo y contraseña; no existe registro público. Las rutas `/admin` están protegidas con autenticación por cookie `HttpOnly`, `Secure` y `SameSite=Lax`, y los intentos de inicio de sesión tienen límite por IP.
- La cookie del navegador contiene un identificador opaco; los tokens de acceso y renovación de Supabase se guardan solo en memoria del proceso del servidor. Si el proceso se reinicia, los organizadores vuelven a iniciar sesión. Este diseño requiere una sola instancia de la aplicación; para varias instancias se debe usar un almacén de sesión distribuido.
- El enlace compartido de invitación usa un token aleatorio de alta entropía; Supabase guarda su SHA-256 para validarlo y una copia cifrada con AES-GCM para que el organizador pueda recuperar el mismo enlace después de reiniciar la aplicación. Vence a los 90 días y renovarlo invalida el enlace previo.
- La clave AES de los enlaces se configura como `InvitationTokens:EncryptionKey`, nunca se guarda en Supabase ni en el repositorio y debe mantenerse estable entre reinicios y despliegues. Si se pierde, el enlace existente no podrá descifrarse y habrá que generar uno nuevo.
- Los enlaces de revelación son individuales, sus tokens se almacenan como hash y vencen a los 90 días. Renovar uno revoca el anterior; por sí solo no revela el resultado.
- La frase privada se transforma a bcrypt con pgcrypto antes de guardarse. La función de revelación aplica 8 intentos fallidos y un bloqueo temporal; nunca devuelve hashes a la aplicación.
- Las frases privadas nuevas deben tener entre 5 y 10 caracteres. La pantalla de revelación sigue aceptando frases anteriores de más de 10 caracteres para no bloquear participantes ya registrados.
- Las operaciones sensibles (registro, cierre, sorteo y lectura privada) pasan por servicios de confianza del servidor. El cliente nunca recibe la tabla completa de asignaciones.
- Las funciones validan el token/credencial, el estado del intercambio y la autorización para cada acción. Aplicar límites de intentos y políticas de expiración. Solo el organizador autenticado puede ver la tabla administrativa de asignaciones; el invitado solo recibe la suya después de validar su frase privada.
- Activar RLS en todas las tablas expuestas. La clave pública no sustituye RLS; no conceder al rol `anon` acceso de lectura a perfiles completos ni asignaciones.
- No se usa la clave `service_role`: la administración usa el JWT de Supabase Auth y las operaciones de invitados solo pueden ejecutar RPCs públicas limitadas. Un resultado privado se devuelve únicamente al participante que validó su enlace y frase privada.
- Serilog escribe eventos estructurados en consola y en `logs/amigo-secreto-YYYYMMDD.log`, con rotación diaria, límite de 10 MB por archivo y retención de 14 archivos. No se habilita el middleware de logging por solicitud; los eventos propios no registran contraseñas, frases privadas, tokens ni contenido de formularios.
- Edad y sexo son datos personales y no son necesarios para sortear. Mantenerlos solo si la familia confirma que los necesita, limitar su exposición y definir eliminación al borrar el intercambio.
- Las rutas de la aplicación se nombran en inglés (`/admin`, `/admin/exchanges`, `/admin/exchange/{exchangeId}`, `/admin/create`, `/join/{exchangeToken}`, `/reveal/{revealToken}`); todo el contenido visible al usuario permanece en español.
- El adaptador de demostración usa SHA-256 solo para emular el flujo sin base de datos; la migración de Supabase usa bcrypt con sal.

## Estados del intercambio

`Draft → RegistrationOpen → ReadyForDraw → Drawn`

`Cancelled` es terminal. Después de `Drawn`, los cambios de participantes requieren cancelar y crear un nuevo sorteo; nunca se debe recalcular silenciosamente una asignación ya consultada.

## Configuración local

El cliente oficial de Supabase y `SupabaseExchangeService` ya están registrados. Para activar la persistencia, primero ejecuta la migración desde **Supabase Dashboard → SQL Editor**:

`supabase/migrations/202611090001_initial_schema.sql`

La migración habilita RLS, revoca el acceso directo a las tablas y concede solo las funciones RPC necesarias. Asegúrate de que la sección Data API exponga el esquema `public` y sus funciones. No pegues una clave secreta en el SQL Editor ni en el repositorio.

Si el esquema inicial ya está aplicado, ejecuta también `supabase/migrations/202611090002_allow_draw_at_minimum.sql` para que Supabase permita cerrar y sortear con tres o más participantes, aunque no se alcance la cantidad prevista.

Aplica después `supabase/migrations/202611090003_fix_reveal_link_row.sql` para actualizar la función de revelación existente.

Aplica `supabase/migrations/202611090004_access_phrase_5_to_10.sql` para validar el nuevo rango al registrar participantes.

Aplica `supabase/migrations/202611100001_persist_invitation_token.sql` para conservar de forma cifrada el enlace de invitación y recuperarlo al volver a abrir el intercambio. Los enlaces creados antes de aplicar esta migración no se pueden recuperar porque solo se guardó su hash; genera uno nuevo después de actualizar.

Configura las credenciales del proyecto para desarrollo con Secret Manager (desde la raíz del repositorio). Crea la cuenta del organizador desde Supabase Auth; esta aplicación no ofrece registro de cuentas:

```powershell
dotnet user-secrets init --project src/AmigoSecretoDigital.Web
dotnet user-secrets set "Supabase:Url" "https://TU-PROYECTO.supabase.co" --project src/AmigoSecretoDigital.Web
dotnet user-secrets set "Supabase:PublishableKey" "sb_publishable_..." --project src/AmigoSecretoDigital.Web
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "InvitationTokens:EncryptionKey" $key --project src/AmigoSecretoDigital.Web
$key = $null
```

En producción, configura `Supabase__Url`, `Supabase__PublishableKey` e `InvitationTokens__EncryptionKey` en el entorno del servidor (en Railway, en **Variables**). La clave de cifrado debe ser la misma para cada despliegue que comparte la misma base de datos. Se usa la clave publicable (o `anon` legacy); nunca configures `service_role` ni una clave `sb_secret_...` en esta aplicación. En `Development`, si Supabase no está configurado, el sitio usa datos temporales de demostración; si Supabase está configurado, también se requiere la clave de cifrado. Fuera de `Development`, Supabase es obligatorio.

## Ejecutar con Docker

El `Dockerfile` compila y publica la aplicación con el SDK .NET 10 y crea una imagen final con solo el runtime ASP.NET Core. El contenedor escucha en el puerto 8080 y ejecuta la app con el usuario no root `app`. `compose.yaml` mantiene los logs de Serilog y las claves de Data Protection en volúmenes Docker.

1. Copia `.env.example` a `.env` y completa `SUPABASE_URL` y `SUPABASE_PUBLISHABLE_KEY` con los valores de tu proyecto.
2. Desde la raíz del repositorio, ejecuta `docker compose up --build -d`.
3. Abre `http://localhost:8080`.

Para ver los logs usa `docker compose logs -f web`; para detener la app usa `docker compose down`. Los volúmenes de logs y Data Protection se conservan al detener el servicio. El estado de las sesiones de organizador permanece en memoria, así que un reinicio del contenedor requiere volver a iniciar sesión. El puerto está ligado a `127.0.0.1` para que el servicio solo responda localmente; para acceso externo, publícalo detrás de un proxy HTTPS.
