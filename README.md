# ECommerceGRPC · Tarea 1 · Arquitectura híbrida REST/gRPC con JWT

El gateway **ApiGateway.BFF** expone REST hacia afuera y habla gRPC hacia adentro. Esta tarea le agrega tres endpoints que responden desde dos servicios gRPC que ya existían (Productos y Usuarios), sin tocar ningún `.proto` ni ningún servicio gRPC.

## Integrantes

| # | Nombre | Carné |
|---|--------|-------|
| 1 | Hubert Hernández Gutiérrez  | _(completar)_ |
| 1 | Minor Quesada Grosso  | _7435_ |
| 1 | William Vargas Morales  | _8937_ |

## Diagrama

```mermaid
flowchart LR
    C["Cliente REST<br/>Swagger · Postman · navegador"] -- "HTTP/1.1 · JSON · JWT" --> G["ApiGateway.BFF :5000<br/>JWT · roles · rate limiting<br/>DTO ⇄ Mapeos.cs"]
    G -- "gRPC · HTTP/2" --> P["ProductService.gRPC :7001<br/>CreateProduct"]
    G -- "gRPC · HTTP/2" --> U["UserService.gRPC :7002<br/>CreateUser · ListUsers (streaming)"]
    G -- "gRPC · HTTP/2" --> O["OrderService.gRPC :7003<br/>ValidateOrder"]
    O --> U
    O --> P
    P --> DB1[("CatalogDB")]
    U --> DB2[("UserDB")]
```

## Cómo levantar el sistema

Requisitos: Docker Desktop y, para correr la colección desde consola, Node.js (opcional).

```bash
cd Infrastructure
docker compose up -d --build     # siempre con --build
docker compose ps                # deben aparecer los siete servicios
```

| Servicio | Puerto en la máquina |
|----------|----------------------|
| Gateway REST + Swagger | `http://localhost:5000/swagger` |
| Productos gRPC | 7001 |
| Usuarios gRPC | 7002 |
| Pedidos gRPC | 7003 |
| Catálogo REST (Nivel 1) | 5001 |
| Usuarios REST (Nivel 1) | 5002 |
| SQL Server | 1434 |

Para apagar: `docker compose down`.

## Usuarios de prueba

| Correo | Clave | Rol |
|--------|-------|-----|
| `admin@example.com` | `Admin2026!` | Admin |
| `user@example.com` | `Cliente2026!` | Cliente |

Se obtiene el token con `POST /api/auth/login` y se envía como `Authorization: Bearer <token>`. En Swagger se pega solo el token en el botón **Authorize**.

## Los tres endpoints

| # | Endpoint | Llama a (gRPC) | Quién puede | Respuestas | Rate limiting |
|---|----------|----------------|-------------|------------|---------------|
| 1 | `POST /api/v1/productos` | `ProductService.CreateProduct` | Rol Admin | 201 · 400 · 401 · 403 · 409 · 429 | `escrituras`: 3 cada 30 s por usuario |
| 2 | `POST /api/v1/usuarios/registro` | `UserService.CreateUser` | Cualquiera, sin token | 201 · 400 · 409 · 429 | `login`: 10 por minuto por IP |
| 3 | `GET /api/v1/usuarios` | `UserService.ListUsers` (server streaming) | Rol Admin | 200 · 401 · 403 · 429 | `lecturas`: 10 cada 10 s por IP |

Los códigos de error los traduce `RpcExceptionHandler`: `InvalidArgument` → 400, `AlreadyExists` → 409, `Unavailable` → 503. Cuando se excede una política responde 429 con el encabezado `Retry-After`.

### 1. Crear producto (Admin)

```http
POST /api/v1/productos
Authorization: Bearer <token de Admin>
Content-Type: application/json

{
  "nombre": "Mouse Swagger 01",
  "descripcion": "Creado desde Swagger UI",
  "precio": 25.5,
  "stock": 40,
  "categoria": "Perifericos"
}
```

```http
HTTP/1.1 201 Created
Location: http://localhost:5000/api/v1/productos/5

{
  "id": 5,
  "nombre": "Mouse Swagger 01",
  "descripcion": "Creado desde Swagger UI",
  "precio": 25.5,
  "categoria": "Perifericos",
  "stock": 40,
  "disponible": true
}
```

Errores: `400` (precio ≤ 0, falta nombre o categoría), `401` (sin token), `403` (token de Cliente), `409` (ya existe un producto con ese nombre).

### 2. Registrar usuario (sin token)

```http
POST /api/v1/usuarios/registro
Content-Type: application/json

{
  "email": "maria.swagger@example.com",
  "nombre": "Maria",
  "apellido": "Lopez",
  "clave": "Clave2026!"
}
```

```http
HTTP/1.1 201 Created
Location: http://localhost:5000/api/v1/usuarios/4

{
  "id": 4,
  "email": "maria.swagger@example.com",
  "nombreCompleto": "Maria Lopez",
  "activo": true,
  "creadoEl": "2026-11-13T06:23:25.1339151Z"
}
```

Errores: `400` (clave de menos de 8 caracteres o datos vacíos), `409` (correo ya registrado). La clave nunca se devuelve; el servicio gRPC guarda solo su hash BCrypt.

### 3. Listar usuarios (Admin)

```http
GET /api/v1/usuarios
Authorization: Bearer <token de Admin>
```

```http
HTTP/1.1 200 OK

[
  { "id": 1, "email": "admin@example.com", "nombreCompleto": "Admin System", "activo": true, "creadoEl": "2026-09-14T00:00:00Z" },
  { "id": 2, "email": "user@example.com",  "nombreCompleto": "John Doe",     "activo": true, "creadoEl": "2026-09-14T00:00:00Z" }
]
```

Por dentro `ListUsers` es server streaming: el gateway junta los mensajes y devuelve una lista JSON. Errores: `401` (sin token), `403` (token de Cliente).

## Estructura del código

| Pieza | Ubicación |
|-------|-----------|
| DTO de solicitud (`CrearProductoDto`, `RegistroDto`) | `ApiGateway.BFF/Modelos/Dtos.cs` |
| Transformaciones DTO → mensaje gRPC (`decimal` → `double`) | `ApiGateway.BFF/Modelos/Mapeos.cs` |
| Endpoints de productos | `ApiGateway.BFF/Controllers/ProductosController.cs` |
| Endpoints de usuarios | `ApiGateway.BFF/Controllers/UsuariosController.cs` |
| Clientes gRPC por inyección de dependencias, rate limiting, JWT | `ApiGateway.BFF/Program.cs` |
| Traducción de errores gRPC → HTTP | `ApiGateway.BFF/Errores/RpcExceptionHandler.cs` |

Ningún controlador arma un mensaje gRPC a mano: todo pasa por `Mapeos`.

## Pruebas con Postman

Importar `Postman/ECommerceGRPC_Clase2.postman_collection.json` y ejecutar **Run collection** con la variable `baseUrl = http://localhost:5000`. La carpeta **6 · Tarea 1** tiene una prueba por cada respuesta de la tabla. Antes de la cuarta escritura espera 31 s (`setTimeout(() => {}, 31000)`) para respetar la política `escrituras`.

Desde consola:

```bash
npx newman run Postman/ECommerceGRPC_Clase2.postman_collection.json
```

## Resiliencia: servicio interno caído

```bash
docker compose stop order-grpc     # apagar Pedidos
# POST /api/v1/pedidos/validar  → 503 "Un servicio interno no responde."
# El resto de los endpoints sigue respondiendo con normalidad.
docker compose start order-grpc    # volver a encenderlo
```

# ¿Cómo viaja un token entre microservicios?

Propagación de identidad en arquitecturas distribuidas: del gateway al último POD de la cadena.
OAuth2 / OIDC | JWT | Service Mesh | Token Exchange

### 1. La idea en una frase

El token no se "guarda" en ningún lado compartido entre PODs. Se reenvía, petición por petición, como parte del contexto de esa llamada específica.

```mermaid
flowchart LR
    Cliente["Cliente"] -->|Bearer token| Gateway["API Gateway<br/><small>valida el JWT</small>"]
    Gateway -->|mismo token| ServicioA["Servicio A<br/><small>POD 1</small>"]
    ServicioA -->|reenvía token| ServicioB["Servicio B<br/><small>POD 2</small>"]

    style Cliente fill:#1c2538,stroke:#2a3550,color:#e8ecf4
    style Gateway fill:#1c2538,stroke:#5b8cff,stroke-width:2px,color:#e8ecf4
    style ServicioA fill:#1c2538,stroke:#36c99e,stroke-width:2px,color:#e8ecf4
    style ServicioB fill:#1c2538,stroke:#b48cff,stroke-width:2px,color:#e8ecf4

```

El mismo `Authorization: Bearer <token>` viaja de salto en salto, en cada request.

### 2. El flujo completo, paso a paso

- **El usuario se autentica** contra el Identity Provider (Keycloak, Auth0, Cognito, Okta...).Recibe un JWT firmado, con claims como `sub`, `scope`, `exp`.
- **El cliente llama a la API** enviando el token en el header.`Authorization: Bearer eyJhbGciOi...`
- **El API Gateway / Ingress valida el token** (firma, expiración, issuer).Puede rechazarlo ahí mismo si está vencido o mal formado — ni siquiera llega a los servicios internos.
- **El Servicio A recibe la request** con el token ya validado y necesita llamar al Servicio B.Un interceptor / middleware copia automáticamente el header entrante al request saliente.
- **El Servicio B valida el token otra vez** (sí, otra vez — cada servicio es responsable de su propia seguridad).Esto se llama _"zero trust"_: no confiar en que "ya alguien lo validó antes".
- **Si hace falta, se pide un token acotado** vía Token Exchange, en vez de pasar el original con todos los permisos.Útil cuando B no debería tener el mismo nivel de acceso que tenía el usuario original.


### 3. Las dos formas de propagarlo

En la práctica vas a ver dos enfoques, y no son mutuamente excluyentes.

| Manual / por código<br>                                                                                                       | Infraestructura                                                                                                       |
| ----------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Interceptores en cada servicio                                                                                                | Service Mesh (Istio, Linkerd)                                                                                         |
| Cada servicio, al hacer su llamada saliente, extrae el token del contexto de la request entrante y lo inyecta en la saliente. | El sidecar (Envoy) se encarga de validar y/o propagar contexto, sin que el código de negocio tenga que pensar en eso. |


### 4. Resumen

|Mecanismo|Cuándo usarlo|
|---|---|
|Reenvío manual del header|Pocos servicios, equipos chicos, control total del código|
|Interceptor / middleware automático|Varios servicios en el mismo stack, querés evitar "se me olvidó"|
|Service Mesh (Istio/Linkerd)|Muchos servicios, en Kubernetes, querés separar identidad de red de identidad de usuario|
|Token Exchange|Cuando un servicio downstream no debería heredar todos los permisos del token original|
|Claims en payload / token de servicio|Comunicación asíncrona (colas, eventos)|
