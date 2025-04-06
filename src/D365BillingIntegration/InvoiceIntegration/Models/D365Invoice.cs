public class D365Invoice
{
    public string CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; }
    public string ExternalInvoiceNumber { get; set; }
    public List<D365InvoiceLine> InvoiceLines { get; set; }
}

public class D365InvoiceLine
{
    public string ItemId { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string TaxGroup { get; set; }
}