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

namespace D365BillingIntegration.CustomerIntegration
{
    public static class CustomerSyncFunction
    {
        [FunctionName("CustomerBusinessEventProcessor")]
        public static async Task Run(
            [ServiceBusTrigger("customer-events", "customer-sync", Connection = "ServiceBusConnection")]
            string eventMessage,
            ILogger log)
        {
            log.LogInformation($"Processing customer event: {eventMessage}");

            try
            {
                // Parse the D365 business event
                var customerEvent = JsonConvert.DeserializeObject<CustomerBusinessEvent>(eventMessage);
                
                // Transform D365 customer data to billing system format
                var billingSystemCustomer = MapToBillingSystemCustomer(customerEvent);
                
                // Call the billing system's .NET assembly
                await ProcessWithBillingSystemAssembly(billingSystemCustomer, log);
                
                log.LogInformation($"Successfully processed customer {customerEvent.CustomerAccount}");
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error processing customer event: {ex.Message}");
                throw; // Will trigger the Azure Function retry policy
            }
        }

        [FunctionName("CustomerSyncManualTrigger")]
        public static async Task<IActionResult> ManualSync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "customer/sync")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Manual customer sync triggered");
            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var customerData = JsonConvert.DeserializeObject<CustomerBusinessEvent>(requestBody);
            
            try
            {
                var billingSystemCustomer = MapToBillingSystemCustomer(customerData);
                await ProcessWithBillingSystemAssembly(billingSystemCustomer, log);
                return new Microsoft.AspNetCore.Mvc.OkObjectResult(new { status = "success" });
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error in manual sync: {ex.Message}");
                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = ex.Message });
            }
        }

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

        private static async Task ProcessWithBillingSystemAssembly(BillingSystemCustomer customer, ILogger log)
        {
            try
            {
                // Load the billing system assembly
                string assemblyPath = Environment.GetEnvironmentVariable("BILLING_SYSTEM_ASSEMBLY_PATH");
                log.LogInformation($"Loading assembly from: {assemblyPath}");
                
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                
                // Create instance of the API wrapper
                var apiType = assembly.GetType("BillingSystem.API.CustomerManager");
                if (apiType == null)
                {
                    throw new Exception("Could not find CustomerManager type in the billing system assembly");
                }
                
                var apiInstance = Activator.CreateInstance(apiType);
                
                // Get the configuration from environment
                string connectionConfig = Environment.GetEnvironmentVariable("BILLING_SYSTEM_CONFIG");
                
                // Initialize the API with connection details
                var initMethod = apiType.GetMethod("Initialize");
                initMethod.Invoke(apiInstance, new object[] { connectionConfig });
                
                // Convert our model to parameters for the billing system API
                string customerJson = JsonConvert.SerializeObject(customer);
                
                // Call the appropriate method based on whether this is new or update
                var method = apiType.GetMethod("UpsertCustomer");
                var result = method.Invoke(apiInstance, new object[] { customerJson });
                
                // Process result
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
                log.LogError(ex, $"Error interacting with billing system assembly: {ex.Message}");
                throw;
            }
        }
    }

    // Models for serialization
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
}