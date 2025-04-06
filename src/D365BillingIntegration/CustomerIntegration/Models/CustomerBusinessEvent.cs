public class CustomerBusinessEvent
{
    public string BusinessEventId { get; set; }
    public string CustomerAccount { get; set; }
    public string CustomerName { get; set; }
    public AddressInfo Address { get; set; }
    public string PrimaryContactEmail { get; set; }
    public string PrimaryContactPhone { get; set; }
    public string CustomerGroupId { get; set; }
    public string TaxExemptNumber { get; set; }
    public int Blocked { get; set; }
}

public class AddressInfo
{
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string CountryRegionId { get; set; }
}