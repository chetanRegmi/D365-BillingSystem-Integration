using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace D365BillingIntegration.Tests.CustomerIntegration
{
    public class CustomerSyncFunctionTest
    {
        private readonly Mock<ILogger> _mockLogger;

        public CustomerSyncFunctionTest()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public async Task Run_ValidEventMessage_ProcessesSuccessfully()
        {
            // Arrange
            var eventMessage = JsonConvert.SerializeObject(new CustomerBusinessEvent
            {
                CustomerAccount = "CUST001",
                CustomerName = "John Doe",
                Address = new AddressInfo
                {
                    AddressLine1 = "123 Main St",
                    City = "Seattle",
                    State = "WA",
                    ZipCode = "98101",
                    CountryRegionId = "US"
                },
                PrimaryContactEmail = "john.doe@example.com",
                PrimaryContactPhone = "123-456-7890",
                CustomerGroupId = "RETAIL",
                TaxExemptNumber = "TAX123",
                Blocked = 0
            });

            // Act
            await CustomerSyncFunction.Run(eventMessage, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(log => log.LogInformation(It.Is<string>(s => s.Contains("Processing customer event"))), Times.Once);
            _mockLogger.Verify(log => log.LogInformation(It.Is<string>(s => s.Contains("Successfully processed customer CUST001"))), Times.Once);
        }

        [Fact]
        public async Task ManualSync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var customerEvent = new CustomerBusinessEvent
            {
                CustomerAccount = "CUST002",
                CustomerName = "Jane Smith",
                Address = new AddressInfo
                {
                    AddressLine1 = "456 Elm St",
                    City = "Portland",
                    State = "OR",
                    ZipCode = "97201",
                    CountryRegionId = "US"
                },
                PrimaryContactEmail = "jane.smith@example.com",
                PrimaryContactPhone = "987-654-3210",
                CustomerGroupId = "WHOLESALE",
                TaxExemptNumber = "TAX456",
                Blocked = 0
            };

            var requestBody = JsonConvert.SerializeObject(customerEvent);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            var httpRequest = httpContext.Request;

            // Act
            var result = await CustomerSyncFunction.ManualSync(httpRequest, _mockLogger.Object) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("success", ((dynamic)result.Value).status);
        }

        [Fact]
        public async Task ManualSync_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var invalidRequestBody = "Invalid JSON";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(invalidRequestBody));
            var httpRequest = httpContext.Request;

            // Act
            var result = await CustomerSyncFunction.ManualSync(httpRequest, _mockLogger.Object) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("error", ((dynamic)result.Value).ToString());
        }
    }
}