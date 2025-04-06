using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Collections.Generic;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace D365BillingIntegration.InvoiceIntegration
{
    public static class InvoiceSyncFunction
    {
        private static readonly HttpClient httpClient = new HttpClient();
        
        /// <summary>
        /// Timer-triggered function that runs nightly to process invoices from the billing system to D365 F&O
        /// </summary>
        [FunctionName("InvoiceNightlyBatchProcessor")]
        public static async Task RunNightlyProcess(
            [TimerTrigger("0 0 2 * * *")] TimerInfo timer, // Runs at 2 AM every day
            ILogger log)
        {
            log.LogInformation($"Invoice nightly batch process started at: {DateTime.Now}");
            
            try
            {
                // Get date range for invoice processing (typically previous day or configurable period)
                DateTime endDate = DateTime.Now.Date;
                DateTime startDate = endDate.AddDays(-1); // Previous day's invoices
                
                // Process invoices in this date range
                await ProcessInvoices(startDate, endDate, log);
                
                log.LogInformation($"Invoice nightly batch process completed successfully at: {DateTime.Now}");
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error in invoice nightly batch process: {ex.Message}");
                throw; // Will trigger the Azure Function retry policy
            }
        }

        /// <summary>
        /// HTTP-triggered function for manual invoice synchronization
        /// </summary>
        [FunctionName("InvoiceSyncManualTrigger")]
        public static async Task<IActionResult> ManualSync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "invoice/sync")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Manual invoice sync triggered");
            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var dateRange = JsonConvert.DeserializeObject<DateRangeRequest>(requestBody);
            
            try
            {
                // Validate date range
                if (dateRange.FromDate == null || dateRange.ToDate == null)
                {
                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                        new { error = "FromDate and ToDate are required" });
                }
                
                // Process invoices for the specified date range
                await ProcessInvoices(dateRange.FromDate.Value, dateRange.ToDate.Value, log);
                
                return new Microsoft.AspNetCore.Mvc.OkObjectResult(new { 
                    status = "success", 
                    message = $"Processed invoices from {dateRange.FromDate:yyyy-MM-dd} to {dateRange.ToDate:yyyy-MM-dd}" 
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error in manual invoice sync: {ex.Message}");
                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Main processing function that handles the invoice synchronization
        /// </summary>
        private static async Task ProcessInvoices(DateTime startDate, DateTime endDate, ILogger log)
        {
            // Step 1: Retrieve invoices from billing system
            var billingSystemInvoices = await GetInvoicesFromBillingSystem(startDate, endDate, log);
            log.LogInformation($"Retrieved {billingSystemInvoices.Count} invoices from billing system");
            
            if (billingSystemInvoices.Count == 0)
            {
                log.LogInformation("No invoices to process");
                return;
            }
            
            // Step 2: Process each invoice and create in D365
            var successfulInvoices = new List<BillingSystemInvoice>();
            var failedInvoices = new List<(BillingSystemInvoice Invoice, string Error)>();
            
            foreach (var invoice in billingSystemInvoices)
            {
                try
                {
                    log.LogInformation($"Processing invoice {invoice.InvoiceNumber} for customer {invoice.CustomerCode}");
                    
                    // Transform billing system invoice to D365 format
                    var d365Invoice = MapToD365Invoice(invoice);
                    
                    // Create invoice in D365
                    var d365InvoiceNumber = await CreateInvoiceInD365(d365Invoice, log);
                    
                    if (!string.IsNullOrEmpty(d365InvoiceNumber))
                    {
                        // Update billing system with D365 invoice number for reference
                        await UpdateBillingSystemWithD365Reference(invoice.InvoiceNumber, d365InvoiceNumber, log);
                        
                        successfulInvoices.Add(invoice);
                        log.LogInformation($"Successfully created invoice {invoice.InvoiceNumber} in D365 with number {d365InvoiceNumber}");
                    }
                    else
                    {
                        failedInvoices.Add((invoice, "Failed to get D365 invoice number"));
                        log.LogWarning($"Failed to get D365 invoice number for {invoice.InvoiceNumber}");
                    }
                }
                catch (Exception ex)
                {
                    failedInvoices.Add((invoice, ex.Message));
                    log.LogError(ex, $"Error processing invoice {invoice.InvoiceNumber}: {ex.Message}");
                    // Continue with next invoice
                }
            }
            
            // Log summary
            log.LogInformation($"Invoice processing summary: {successfulInvoices.Count} successful, {failedInvoices.Count} failed");
            
            // If there are failed invoices, log details for troubleshooting
            foreach (var failure in failedInvoices)
            {
                log.LogWarning($"Failed invoice {failure.Invoice.InvoiceNumber}: {failure.Error}");
            }
        }

        /// <summary>
        /// Retrieve invoices from the billing system using its .NET assembly
        /// </summary>
        private static async Task<List<BillingSystemInvoice>> GetInvoicesFromBillingSystem(
            DateTime startDate, DateTime endDate, ILogger log)
        {
            try
            {
                // Load the billing system assembly
                string assemblyPath = Environment.GetEnvironmentVariable("BILLING_SYSTEM_ASSEMBLY_PATH");
                log.LogInformation($"Loading assembly from: {assemblyPath}");
                
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                
                // Create instance of the API wrapper
                var apiType = assembly.GetType("BillingSystem.API.InvoiceManager");
                if (apiType == null)
                {
                    throw new Exception("Could not find InvoiceManager type in the billing system assembly");
                }
                
                var apiInstance = Activator.CreateInstance(apiType);
                
                // Get the configuration from environment
                string connectionConfig = Environment.GetEnvironmentVariable("BILLING_SYSTEM_CONFIG");
                
                // Initialize the API with connection details
                var initMethod = apiType.GetMethod("Initialize");
                initMethod.Invoke(apiInstance, new object[] { connectionConfig });
                
                // Create parameters for retrieving invoices
                string startDateStr = startDate.ToString("yyyy-MM-dd");
                string endDateStr = endDate.ToString("yyyy-MM-dd");
                bool includeProcessed = false; // Only get invoices that haven't been processed yet
                
                // Call the appropriate method to get invoices
                var method = apiType.GetMethod("GetInvoicesForDateRange");
                var result = method.Invoke(apiInstance, new object[] { startDateStr, endDateStr, includeProcessed });
                
                // Process result - should be a JSON string containing invoices
                string invoicesJson = (string)result;
                var invoices = JsonConvert.DeserializeObject<List<BillingSystemInvoice>>(invoicesJson);
                
                // Cleanup
                var disposeMethod = apiType.GetMethod("Dispose");
                disposeMethod?.Invoke(apiInstance, null);
                
                return invoices ?? new List<BillingSystemInvoice>();
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error retrieving invoices from billing system: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Map billing system invoice to D365 invoice format
        /// </summary>
        private static D365Invoice MapToD365Invoice(BillingSystemInvoice billingInvoice)
        {
            // Create header
            var d365Invoice = new D365Invoice
            {
                CustomerId = billingInvoice.CustomerCode,
                InvoiceDate = billingInvoice.InvoiceDate,
                DueDate = billingInvoice.DueDate,
                CurrencyCode = billingInvoice.CurrencyCode,
                ExternalInvoiceNumber = billingInvoice.InvoiceNumber,
                InvoiceLines = new List<D365InvoiceLine>()
            };
            
            // Map each line item
            foreach (var lineItem in billingInvoice.LineItems)
            {
                d365Invoice.InvoiceLines.Add(new D365InvoiceLine
                {
                    ItemId = lineItem.ProductCode,
                    Description = lineItem.Description,
                    Quantity = lineItem.Quantity,
                    UnitPrice = lineItem.UnitPrice,
                    DiscountAmount = lineItem.DiscountAmount,
                    TaxAmount = lineItem.TaxAmount,
                    TaxGroup = MapTaxGroup(lineItem.TaxRate)
                });
            }
            
            return d365Invoice;
        }
        
        /// <summary>
        /// Map tax rate to D365 tax group
        /// </summary>
        private static string MapTaxGroup(decimal taxRate)
        {
            // Simple mapping of tax rates to D365 tax groups
            // In a real implementation, this would be more sophisticated or configurable
            return taxRate switch
            {
                0 => "EXEMPT",
                5 => "GST5",
                7 => "VAT7",
                _ => "STANDARD"
            };
        }
        
        /// <summary>
        /// Create invoice in D365 using OData API
        /// </summary>
        private static async Task<string> CreateInvoiceInD365(D365Invoice invoice, ILogger log)
        {
            try
            {
                // Get D365 configuration
                string d365Url = Environment.GetEnvironmentVariable("D365_API_URL");
                string tenantId = Environment.GetEnvironmentVariable("D365_TENANT_ID");
                string clientId = Environment.GetEnvironmentVariable("D365_CLIENT_ID");
                string clientSecret = Environment.GetEnvironmentVariable("D365_CLIENT_SECRET");
                
                // Get OAuth token for D365
                string token = await GetD365AuthToken(tenantId, clientId, clientSecret);
                
                // Prepare API URL - using Customer Invoice Journal as an example
                string apiUrl = $"{d365Url}/data/CustomerInvoiceHeaders";
                
                // Prepare request
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                // Convert invoice to JSON and add to request body
                string jsonContent = JsonConvert.SerializeObject(invoice, 
                    new JsonSerializerSettings { 
                        NullValueHandling = NullValueHandling.Ignore,
                        DateFormatString = "yyyy-MM-dd"
                    });
                
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Send request to D365
                var response = await httpClient.SendAsync(request);
                
                // Check if successful
                if (response.IsSuccessStatusCode)
                {
                    // Parse response to get D365 invoice number
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var invoiceResponse = JsonConvert.DeserializeObject<D365InvoiceResponse>(responseContent);
                    
                    return invoiceResponse?.InvoiceNumber;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    log.LogError($"D365 API error: {response.StatusCode} - {errorContent}");
                    throw new Exception($"Failed to create invoice in D365: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error creating invoice in D365: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Get OAuth token for Dynamics 365 API
        /// </summary>
        private static async Task<string> GetD365AuthToken(string tenantId, string clientId, string clientSecret)
        {
            var authContext = new AuthenticationContext($"https://login.microsoftonline.com/{tenantId}");
            var credential = new ClientCredential(clientId, clientSecret);
            var result = await authContext.AcquireTokenAsync("https://d365ffo.onmicrosoft.com", credential);
            return result.AccessToken;
        }
        
        /// <summary>
        /// Update billing system with D365 invoice number reference
        /// </summary>
        private static async Task UpdateBillingSystemWithD365Reference(
            string billingInvoiceNumber, string d365InvoiceNumber, ILogger log)
        {
            try
            {
                // Load the billing system assembly
                string assemblyPath = Environment.GetEnvironmentVariable("BILLING_SYSTEM_ASSEMBLY_PATH");
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                
                // Create instance of the API wrapper
                var apiType = assembly.GetType("BillingSystem.API.InvoiceManager");
                var apiInstance = Activator.CreateInstance(apiType);
                
                // Get the configuration from environment
                string connectionConfig = Environment.GetEnvironmentVariable("BILLING_SYSTEM_CONFIG");
                
                // Initialize the API with connection details
                var initMethod = apiType.GetMethod("Initialize");
                initMethod.Invoke(apiInstance, new object[] { connectionConfig });
                
                // Call the method to update invoice reference
                var updateMethod = apiType.GetMethod("UpdateInvoiceERPReference");
                var result = updateMethod.Invoke(apiInstance, new object[] { billingInvoiceNumber, d365InvoiceNumber });
                
                // Check result
                bool success = (bool)result;
                if (!success)
                {
                    var lastErrorMethod = apiType.GetMethod("GetLastError");
                    string error = (string)lastErrorMethod.Invoke(apiInstance, null);
                    throw new Exception($"Billing system error: {error}");
                }
                
                // Cleanup
                var disposeMethod = apiType.GetMethod("Dispose");
                disposeMethod?.Invoke(apiInstance, null);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error updating billing system with D365 reference: {ex.Message}");
                throw;
            }
        }
    }
    
    // Request model for manual sync
    public class DateRangeRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
    
    // Models for billing system invoice
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
    
    // Models for D365 invoice
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
    
    // Response model from D365
    public class D365InvoiceResponse
    {
        public string InvoiceNumber { get; set; }
        public string RecId { get; set; }
    }
}