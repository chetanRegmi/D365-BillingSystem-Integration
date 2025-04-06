public class BillingSystemCustomer
{
    public string CustomerCode { get; set; }
    public string CustomerName { get; set; }
    public BillingSystemAddress Address { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string CustomerType { get; set; }
    public string TaxId { get; set; }
    public bool IsActive { get; set; }
}

public class BillingSystemAddress
{
    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
}