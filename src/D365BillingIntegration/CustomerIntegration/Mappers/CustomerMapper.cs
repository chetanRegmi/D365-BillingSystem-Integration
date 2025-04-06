private static BillingSystemCustomer MapToBillingSystemCustomer(CustomerBusinessEvent d365Customer)
{
    // Transform D365 customer structure to billing system structure
    return new BillingSystemCustomer
    {
        CustomerCode = d365Customer.CustomerAccount,
        CustomerName = d365Customer.CustomerName,
        Address = new BillingSystemAddress
        {
            Line1 = d365Customer.Address?.AddressLine1,
            Line2 = d365Customer.Address?.AddressLine2,
            City = d365Customer.Address?.City,
            State = d365Customer.Address?.State,
            PostalCode = d365Customer.Address?.ZipCode,
            Country = d365Customer.Address?.CountryRegionId
        },
        Email = d365Customer.PrimaryContactEmail,
        Phone = d365Customer.PrimaryContactPhone,
        CustomerType = MapCustomerType(d365Customer.CustomerGroupId),
        TaxId = d365Customer.TaxExemptNumber,
        IsActive = d365Customer.Blocked == 0
    };
}

private static string MapCustomerType(string d365CustomerGroup)
{
    // Map D365 customer groups to billing system customer types
    return d365CustomerGroup switch
    {
        "RETAIL" => "B2C",
        "WHOLESALE" => "B2B",
        "CORPORATE" => "B2B-LARGE",
        _ => "STANDARD"
    };
}