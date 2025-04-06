using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace D365BillingIntegration.Shared.Logging
{
    /// <summary>
    /// Extension methods for ILogger that provide structured logging
    /// with consistent formats for the D365-BillingSystem integration.
    /// </summary>
    public static class LoggingExtensions
    {
        #region Customer Integration Logging

        /// <summary>
        /// Logs the start of customer synchronization
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="customerAccount">The customer account ID</param>
        /// <param name="eventId">Optional business event ID</param>
        public static void LogCustomerSyncStart(this ILogger logger, string customerAccount, string eventId = null)
        {
            logger.LogInformation(
                "Starting customer synchronization for customer {CustomerAccount}. Event ID: {EventId}",
                customerAccount,
                eventId ?? "Manual Trigger");
        }

        /// <summary>
        /// Logs the successful completion of customer synchronization
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="customerAccount">The customer account ID</param>
        /// <param name="durationMs">Duration of the operation in milliseconds</param>
        public static void LogCustomerSyncSuccess(this ILogger logger, string customerAccount, long durationMs)
        {
            logger.LogInformation(
                "Successfully synchronized customer {CustomerAccount}. Operation took {DurationMs}ms",
                customerAccount,
                durationMs);
        }

        /// <summary>
        /// Logs a failed customer synchronization operation
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="customerAccount">The customer account ID</param>
        /// <param name="ex">The exception that caused the failure</param>
        public static void LogCustomerSyncFailure(this ILogger logger, string customerAccount, Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to synchronize customer {CustomerAccount}. Error: {ErrorMessage}",
                customerAccount,
                ex.Message);
        }

        #endregion

        #region Invoice Integration Logging

        /// <summary>
        /// Logs the start of invoice batch processing
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="startDate">Start date of the invoice range</param>
        /// <param name="endDate">End date of the invoice range</param>
        public static void LogInvoiceBatchStart(this ILogger logger, DateTime startDate, DateTime endDate)
        {
            logger.LogInformation(
                "Starting invoice batch processing for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate,
                endDate);
        }

        /// <summary>
        /// Logs the completion of invoice batch processing with statistics
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="totalCount">Total number of invoices processed</param>
        /// <param name="successCount">Number of successfully processed invoices</param>
        /// <param name="failureCount">Number of failed invoices</param>
        /// <param name="durationMs">Duration of the operation in milliseconds</param>
        public static void LogInvoiceBatchComplete(
            this ILogger logger, 
            int totalCount, 
            int successCount, 
            int failureCount, 
            long durationMs)
        {
            logger.LogInformation(
                "Invoice batch processing completed. Total: {TotalCount}, Success: {SuccessCount}, " +
                "Failed: {FailureCount}. Operation took {DurationMs}ms",
                totalCount,
                successCount,
                failureCount,
                durationMs);
        }

        /// <summary>
        /// Logs individual invoice processing
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="invoiceNumber">The billing system invoice number</param>
        /// <param name="customerAccount">The customer account ID</param>
        public static void LogInvoiceProcessing(this ILogger logger, string invoiceNumber, string customerAccount)
        {
            logger.LogInformation(
                "Processing invoice {InvoiceNumber} for customer {CustomerAccount}",
                invoiceNumber,
                customerAccount);
        }

        /// <summary>
        /// Logs successful invoice creation in D365
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="billingInvoiceNumber">The billing system invoice number</param>
        /// <param name="d365InvoiceNumber">The D365 invoice number</param>
        public static void LogInvoiceCreationSuccess(
            this ILogger logger, 
            string billingInvoiceNumber, 
            string d365InvoiceNumber)
        {
            logger.LogInformation(
                "Successfully created invoice {BillingInvoiceNumber} in D365 with number {D365InvoiceNumber}",
                billingInvoiceNumber,
                d365InvoiceNumber);
        }

        /// <summary>
        /// Logs failed invoice creation
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="invoiceNumber">The billing system invoice number</param>
        /// <param name="ex">The exception that caused the failure</param>
        public static void LogInvoiceCreationFailure(this ILogger logger, string invoiceNumber, Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to create invoice {InvoiceNumber} in D365. Error: {ErrorMessage}",
                invoiceNumber,
                ex.Message);
        }

        #endregion

        #region Integration System Logging

        /// <summary>
        /// Logs assembly loading operations
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="assemblyPath">Path to the assembly being loaded</param>
        public static void LogAssemblyLoading(this ILogger logger, string assemblyPath)
        {
            logger.LogInformation(
                "Loading assembly from {AssemblyPath}",
                assemblyPath);
        }

        /// <summary>
        /// Logs D365 API operations
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="operation">Name of the operation</param>
        /// <param name="endpoint">API endpoint</param>
        public static void LogD365ApiOperation(this ILogger logger, string operation, string endpoint)
        {
            logger.LogInformation(
                "D365 API {Operation} to {Endpoint}",
                operation,
                endpoint);
        }

        /// <summary>
        /// Logs authentication operations
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="system">The system being authenticated with</param>
        /// <param name="clientId">The client ID used (partially masked)</param>
        public static void LogAuthentication(this ILogger logger, string system, string clientId)
        {
            // Mask client ID for security
            string maskedClientId = clientId;
            if (clientId?.Length > 8)
            {
                maskedClientId = clientId.Substring(0, 4) + "****" + clientId.Substring(clientId.Length - 4);
            }

            logger.LogInformation(
                "Authenticating with {System} using client ID {ClientId}",
                system,
                maskedClientId);
        }

        /// <summary>
        /// Logs integration configuration
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="configName">Configuration property name</param>
        /// <param name="configValue">Configuration value (sensitive data should be masked before calling)</param>
        public static void LogConfiguration(this ILogger logger, string configName, string configValue)
        {
            logger.LogInformation(
                "Configuration: {ConfigName} = {ConfigValue}",
                configName,
                configValue);
        }

        /// <summary>
        /// Logs integration metrics for monitoring
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="operation">The operation being measured</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="additionalData">Any additional metric data</param>
        public static void LogPerformanceMetric(
            this ILogger logger, 
            string operation, 
            long durationMs, 
            Dictionary<string, object> additionalData = null)
        {
            // Create a loggable object that combines all metrics
            var metrics = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["DurationMs"] = durationMs
            };

            // Add any additional metrics
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    metrics[kvp.Key] = kvp.Value;
                }
            }

            logger.LogInformation(
                "Performance: {Operation} completed in {DurationMs}ms", 
                operation, 
                durationMs);
        }

        #endregion
    }
}