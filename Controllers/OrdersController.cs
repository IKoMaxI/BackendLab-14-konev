using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StoreApiLR11.Data;
using StoreApiLR11.DTOs.Order;
using StoreApiLR11.Extensions;
using StoreApiLR11.Models;

namespace StoreApiLR11.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(StoreContext context, ILogger<OrdersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
    {
        var orders = await context.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.OrderItems)
            .ThenInclude(item => item.Product)
            .OrderByDescending(order => order.OrderDate)
            .ToListAsync();

        return Ok(orders.Select(order => order.ToDto()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await context.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.OrderItems)
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);

        return order is null ? NotFound() : Ok(order.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationProblem());
        }

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<ActionResult<OrderDto>>(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var customer = await context.Customers.FindAsync(dto.CustomerId);
                if (customer is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Customer not found",
                        Detail = $"Customer with Id={dto.CustomerId} was not found.",
                        Instance = HttpContext.Request.Path
                    });
                }

                var order = new Order
                {
                    CustomerId = dto.CustomerId,
                    Customer = customer,
                    OrderDate = DateTime.UtcNow,
                    Status = "Pending",
                    OrderItems = []
                };

                decimal total = 0;

                foreach (var itemDto in dto.Items)
                {
                    var product = await context.Products.FindAsync(itemDto.ProductId);

                    if (product is null)
                    {
                        return NotFound(new ProblemDetails
                        {
                            Status = StatusCodes.Status404NotFound,
                            Title = "Product not found",
                            Detail = $"Product with Id={itemDto.ProductId} was not found.",
                            Instance = HttpContext.Request.Path
                        });
                    }

                    if (product.Stock < itemDto.Quantity)
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Title = "Insufficient stock",
                            Detail = $"Not enough stock for \"{product.Name}\". Available: {product.Stock}, requested: {itemDto.Quantity}.",
                            Instance = HttpContext.Request.Path
                        });
                    }

                    product.Stock -= itemDto.Quantity;

                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = itemDto.ProductId,
                        Product = product,
                        Quantity = itemDto.Quantity,
                        Price = product.Price
                    });

                    total += itemDto.Quantity * product.Price;
                }

                order.TotalAmount = total;
                context.Orders.Add(order);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                logger.LogInformation(
                    "Order {OrderId} created for customer {CustomerId} with total {TotalAmount}",
                    order.Id,
                    order.CustomerId,
                    order.TotalAmount);

                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order.ToDto());
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private ProblemDetails CreateValidationProblem()
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred.",
            Instance = HttpContext.Request.Path,
            Extensions = { ["errors"] = ModelState }
        };
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, UpdateOrderStatusDto dto)
    {
        var order = await context.Orders.FindAsync(id);
        if (order is null) return NotFound();

        order.Status = dto.Status;
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await context.Orders.FindAsync(id);
        if (order is null) return NotFound();

        order.IsDeleted = true;
        order.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return NoContent();
    }
}
