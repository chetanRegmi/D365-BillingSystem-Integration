using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace D365BillingIntegration.Shared.Configuration
{
    /// <summary>
    /// Manages configuration settings for the D365-BillingSystem integration.
    /// Handles loading configuration from environment variables, Azure Key Vault, 
    /// or configuration files with appropriate security measures.
    /// </summary>
    public class IntegrationSettings
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _settings;
        
        // Required settings that must be available for the integration to work
        private static readonly string[] _requiredSettings = new[]
        {
            "ServiceBusConnection",
            "BILLING_SYSTEM_ASSEMBLY_PATH",
            "BILLING_SYSTEM_CONFIG",
            "D365_API_URL",
            "D365_TENANT_ID",
            "D365_CLIENT_ID",
            "D365_CLIENT_SECRET"
        };

        /// <summary>
        /// Initialize settings from environment variables
        /// </summary>
        /// <param name="logger">Logger instance for diagnostics</param>
        public IntegrationSettings(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            LoadFromEnvironment();
        }

        /// <summary>
        /// Initialize settings from a provided configuration source
        /// </summary>
        /// <param name="configuration">IConfiguration instance</param>
        /// <param name="logger">Logger instance for diagnostics</param>
        public IntegrationSettings(IConfiguration configuration, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
                
            _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            LoadFromConfiguration(configuration);
        }

        #region Customer Integration Settings

        /// <summary>
        /// Azure Service Bus connection string
        /// </summary>
        public string ServiceBusConnection => GetSetting("ServiceBusConnection");
        
        /// <summary>
        /// Service bus queue name for customer events
        /// </summary>
        public string CustomerEventsQueue => GetSetting("CustomerEventsQueue", "customer-events");
        
        /// <summary>
        /// Service bus subscription name for customer sync
        /// </summary>
        public string CustomerSyncSubscription => GetSetting("CustomerSyncSubscription", "customer-sync");

        #endregion

        #region Invoice Integration Settings

        /// <summary>
        /// CRON expression for the invoice sync timer trigger
        /// </summary>
        public string InvoiceSyncSchedule => GetSetting("InvoiceSyncSchedule", "0 0 2 * * *"); // Default: 2 AM daily
        
        /// <summary>
        /// Default number of days to look back for invoices if not specified
        /// </summary>
        public int DefaultInvoiceLookbackDays
        {
            get
            {
                if (int.TryParse(GetSetting("DefaultInvoiceLookbackDays", "1"), out int days))
                    return days;
                return 1;
            }
        }

        #endregion

        #region Billing System Settings

        /// <summary>
        /// Path to the billing system assembly (.dll)
        /// </summary>
        public string BillingSystemAssemblyPath => GetSetting("BILLING_SYSTEM_ASSEMBLY_PATH");
        
        /// <summary>
        /// Billing system configuration (JSON format)
        /// </summary>
        public string BillingSystemConfig => GetSetting("BILLING_SYSTEM_CONFIG");
        
        /// <summary>
        /// Gets the billing system configuration as a typed object
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <returns>Configuration object</returns>
        public T GetBillingSystemConfig<T>() where T : class, new()
        {
            try
            {
                string config = BillingSystemConfig;
                if (string.IsNullOrWhiteSpace(config))
                    return new T();
                    
                return JsonConvert.DeserializeObject<T>(config) ?? new T();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing billing system configuration: {ErrorMessage}", ex.Message);
                return new T();
            }
        }

        #endregion

        #region D365 Settings

        /// <summary>
        /// D365 F&O API URL
        /// </summary>
        public string D365ApiUrl => GetSetting("D365_API_URL");
        
        /// <summary>
        /// Azure AD tenant ID for D365 authentication
        /// </summary>
        public string D365TenantId => GetSetting("D365_TENANT_ID");
        
        /// <summary>
        /// Client ID for D365 authentication
        /// </summary>
        public string D365ClientId => GetSetting("D365_CLIENT_ID");
        
        /// <summary>
        /// Client secret for D365 authentication
        /// </summary>
        public SecureString D365ClientSecret
        {
            get
            {
                string secret = GetSetting("D365_CLIENT_SECRET");
                if (string.IsNullOrEmpty(secret))
                    return new SecureString();
                    
                var secureSecret = new SecureString();
                foreach (char c in secret)
                {
                    secureSecret.AppendChar(c);
                }
                secureSecret.MakeReadOnly();
                return secureSecret;
            }
        }

        #endregion

        #region Application Settings

        /// <summary>
        /// Application Insights instrumentation key for telemetry
        /// </summary>
        public string InstrumentationKey => GetSetting("APPINSIGHTS_INSTRUMENTATIONKEY");
        
        /// <summary>
        /// Enable detailed logging
        /// </summary>
        public bool DetailedLogging
        {
            get
            {
                if (bool.TryParse(GetSetting("DetailedLogging", "false"), out bool result))
                    return result;
                return false;
            }
        }
        
        /// <summary>
        /// Maximum retry attempts for operations
        /// </summary>
        public int MaxRetryAttempts
        {
            get
            {
                if (int.TryParse(GetSetting("MaxRetryAttempts", "3"), out int attempts))
                    return attempts;
                return 3;
            }
        }

        #endregion

        #region Configuration Loading

        /// <summary>
        /// Load settings from environment variables
        /// </summary>
        private void LoadFromEnvironment()
        {
            _logger.LogInformation("Loading settings from environment variables");
            
            foreach (var variable in Environment.GetEnvironmentVariables().Keys)
            {
                string key = variable.ToString();
                _settings[key] = Environment.GetEnvironmentVariable(key);
            }
            
            ValidateRequiredSettings();
        }
        
        /// <summary>
        /// Load settings from IConfiguration source
        /// </summary>
        private void LoadFromConfiguration(IConfiguration configuration)
        {
            _logger.LogInformation("Loading settings from configuration");
            
            foreach (var setting in configuration.AsEnumerable())
            {
                if (!string.IsNullOrEmpty(setting.Key))
                {
                    _settings[setting.Key] = setting.Value;
                }
            }
            
            ValidateRequiredSettings();
        }
        
        /// <summary>
        /// Load settings from a JSON file
        /// </summary>
        /// <param name="filePath">Path to the JSON configuration file</param>
        public void LoadFromJsonFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
                
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
                
            _logger.LogInformation("Loading settings from JSON file: {FilePath}", filePath);
            
            try
            {
                string json = File.ReadAllText(filePath);
                var jsonSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                
                if (jsonSettings != null)
                {
                    foreach (var kvp in jsonSettings)
                    {
                        _settings[kvp.Key] = kvp.Value;
                    }
                }
                
                ValidateRequiredSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings from JSON file: {ErrorMessage}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Validates that all required settings are available
        /// </summary>
        private void ValidateRequiredSettings()
        {
            var missingSettings = _requiredSettings
                .Where(key => string.IsNullOrEmpty(GetSetting(key)))
                .ToList();
                
            if (missingSettings.Any())
            {
                string missingSetting = string.Join(", ", missingSettings);
                _logger.LogError("Missing required settings: {MissingSettings}", missingSetting);
                throw new ConfigurationException($"Missing required settings: {missingSetting}");
            }
            
            _logger.LogInformation("All required settings validated successfully");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get a setting by key with an optional default value
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Optional default value</param>
        /// <returns>Setting value or default</returns>
        public string GetSetting(string key, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Setting key cannot be null or empty", nameof(key));
                
            return _settings.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value)
                ? value
                : defaultValue;
        }
        
        /// <summary>
        /// Updates a setting value
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <param name="value">New value</param>
        public void UpdateSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Setting key cannot be null or empty", nameof(key));
                
            _settings[key] = value;
            _logger.LogInformation("Updated setting: {SettingKey}", key);
        }
        
        /// <summary>
        /// Gets all settings (excluding secrets)
        /// </summary>
        /// <returns>Dictionary of non-secret settings</returns>
        public Dictionary<string, string> GetAllSettings()
        {
            // Create a new dictionary with all settings except secrets
            var nonSecretSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // List of keys that should not be exposed (contains secrets)
            var secretKeys = new[] { 
                "D365_CLIENT_SECRET", 
                "BILLING_SYSTEM_CONFIG",
                "ServiceBusConnection"
            };
            
            foreach (var kvp in _settings)
            {
                if (!secretKeys.Any(s => kvp.Key.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    nonSecretSettings[kvp.Key] = kvp.Value;
                }
                else
                {
                    // For secret keys, indicate they exist but don't show value
                    nonSecretSettings[kvp.Key] = "[SECRET]";
                }
            }
            
            return nonSecretSettings;
        }

        #endregion
    }
    
    /// <summary>
    /// Exception thrown when configuration validation fails
    /// </summary>
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
        
        public ConfigurationException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}