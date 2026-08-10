namespace OrderSystem.Domain.Entities;

public class Address
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}
