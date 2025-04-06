using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace D365BillingIntegration.Tests.InvoiceIntegration
{
    public class InvoiceSyncFunctionTest
    {
        private readonly Mock<ILogger> _mockLogger;

        public InvoiceSyncFunctionTest()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public async Task RunNightlyProcess_ValidExecution_LogsSuccess()
        {
            // Arrange
            var timerInfo = new TimerInfo(null, null, false);

            // Act
            await InvoiceSyncFunction.RunNightlyProcess(timerInfo, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(log => log.LogInformation(It.Is<string>(s => s.Contains("Invoice nightly batch process started"))), Times.Once);
            _mockLogger.Verify(log => log.LogInformation(It.Is<string>(s => s.Contains("Invoice nightly batch process completed successfully"))), Times.Once);
        }

        [Fact]
        public async Task ManualSync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var dateRangeRequest = new DateRangeRequest
            {
                FromDate = DateTime.Now.AddDays(-1),
                ToDate = DateTime.Now
            };

            var requestBody = JsonConvert.SerializeObject(dateRangeRequest);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            var httpRequest = httpContext.Request;

            // Act
            var result = await InvoiceSyncFunction.ManualSync(httpRequest, _mockLogger.Object) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
            Assert.Contains("Processed invoices", ((dynamic)result.Value).message);
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
            var result = await InvoiceSyncFunction.ManualSync(httpRequest, _mockLogger.Object) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("error", ((dynamic)result.Value).ToString());
        }

        [Fact]
        public async Task ProcessInvoices_NoInvoices_LogsNoInvoices()
        {
            // Arrange
            var startDate = DateTime.Now.AddDays(-1);
            var endDate = DateTime.Now;

            // Mock the GetInvoicesFromBillingSystem method to return an empty list
            var mockInvoices = new List<BillingSystemInvoice>();
            var mockFunction = new Mock<InvoiceSyncFunction>();
            mockFunction.Setup(f => f.GetInvoicesFromBillingSystem(startDate, endDate, _mockLogger.Object))
                        .ReturnsAsync(mockInvoices);

            // Act
            await InvoiceSyncFunction.ProcessInvoices(startDate, endDate, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(log => log.LogInformation(It.Is<string>(s => s.Contains("No invoices to process"))), Times.Once);
        }
    }
}