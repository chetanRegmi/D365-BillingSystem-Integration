public class BillingSystemInvoice
{
    public string InvoiceNumber { get; set; }
    public string CustomerCode { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string CurrencyCode { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public List<BillingSystemLineItem> LineItems { get; set; }
    public string ERPReference { get; set; } // For storing the D365 invoice number
}

public class BillingSystemLineItem
{
    public string ProductCode { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TaxRate { get; set; }
}