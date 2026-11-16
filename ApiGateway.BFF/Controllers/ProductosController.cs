using ApiGateway.BFF.Modelos;
using ECommerceGRPC.Products;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiGateway.BFF.Controllers;

// REST afuera, gRPC adentro: cada acción recibe JSON, llama a ProductService.gRPC (7001)
// y devuelve JSON. Los errores gRPC los traduce RpcExceptionHandler.
[ApiController]
[Route("api/v1/productos")]
public class ProductosController(ProductService.ProductServiceClient productos) : ControllerBase
{
    /// <summary>Lista los productos activos. Opcional: filtrar por categoría.</summary>
    /// <remarks>Por dentro llama a ListProducts, que es server streaming: el gateway junta los mensajes en una lista JSON.</remarks>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("lecturas")]
    [ProducesResponseType<List<ProductoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<List<ProductoDto>>> Listar([FromQuery] string? categoria, CancellationToken ct)
    {
        using var llamada = productos.ListProducts(new ListProductsRequest { Category = categoria ?? "" }, cancellationToken: ct);
        var lista = new List<ProductoDto>();
        await foreach (var p in llamada.ResponseStream.ReadAllAsync(ct))
            lista.Add(Mapeos.ADto(p));
        return Ok(lista);
    }

    /// <summary>Un producto por id.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [EnableRateLimiting("lecturas")]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> Obtener(int id, CancellationToken ct)
    {
        // TODO 2 · Llame a productos.GetProductAsync con un GetProductRequest { Id = id }
        //   y devuelva Ok(Mapeos.ADto(reply)). Si no existe, gRPC responde NotFound
        //   y RpcExceptionHandler lo convierte en 404: no hace falta un try/catch.
        var reply = await productos.GetProductAsync(new GetProductRequest { Id = id }, cancellationToken: ct);
        return Ok(Mapeos.ADto(reply));
    }

    /// <summary>Crea un producto en el catálogo. Solo el rol Admin.</summary>
    /// <remarks>Por dentro llama a CreateProduct. Responde 201 con la ruta del GET por id en el encabezado Location.
    /// 400 si faltan datos o el precio no es mayor que 0; 409 si ya existe un producto con ese nombre.</remarks>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [EnableRateLimiting("escrituras")]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ProductoDto>> Crear(CrearProductoDto dto, CancellationToken ct)
    {
        var reply = await productos.CreateProductAsync(Mapeos.ACreateProduct(dto), cancellationToken: ct);
        return CreatedAtAction(nameof(Obtener), new { id = reply.Id }, Mapeos.ADto(reply));
    }

    /// <summary>Cambia el stock de un producto. Solo el rol Admin.</summary>
    /// <remarks>Por dentro llama a UpdateStock, que además avisa a quien esté mirando WatchStock.</remarks>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/stock")]
    [EnableRateLimiting("escrituras")]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ProductoDto>> ActualizarStock(int id, ActualizarStockDto dto, CancellationToken ct)
    {
        // TODO 5 · Proteja esta acción con [Authorize(Roles = "Admin")] (arriba de [HttpPut]).
        //   Después llame a productos.UpdateStockAsync(Mapeos.AUpdateStock(id, dto))
        //   y devuelva Ok(Mapeos.ADto(reply)).
        var reply = await productos.UpdateStockAsync(Mapeos.AUpdateStock(id, dto), cancellationToken: ct);
        return Ok(Mapeos.ADto(reply));
    }
}
