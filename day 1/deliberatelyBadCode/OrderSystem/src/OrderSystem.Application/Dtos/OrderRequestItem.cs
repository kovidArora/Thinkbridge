using System.ComponentModel.DataAnnotations;

namespace OrderSystem.Application.Dtos;

public class OrderRequestItem
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid product id is required.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public int Quantity { get; set; }
}
