using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace D365BillingIntegration.Shared
{
    /// 
    /// Utility class to handle dynamic loading of third-party assemblies
    /// with proper exception handling and logging
    /// 
    public class AssemblyLoader
    {
        private readonly ILogger _logger;
        
        public AssemblyLoader(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// 
        /// Loads an assembly from the specified path
        /// 
        /// Full path to the assembly file
        /// The loaded assembly
        public Assembly LoadAssembly(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                throw new ArgumentException("Assembly path cannot be null or empty", nameof(assemblyPath));
            }
            
            if (!File.Exists(assemblyPath))
            {
                _logger.LogError($"Assembly file not found at: {assemblyPath}");
                throw new FileNotFoundException($"Assembly file not found at: {assemblyPath}");
            }
            
            try
            {
                _logger.LogInformation($"Loading assembly from: {assemblyPath}");
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            }
            catch (BadImageFormatException ex)
            {
                _logger.LogError(ex, $"File is not a valid assembly: {assemblyPath}");
                throw;
            }
            catch (FileLoadException ex)
            {
                _logger.LogError(ex, $"Assembly file exists but could not be loaded: {assemblyPath}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error loading assembly: {assemblyPath}");
                throw;
            }
        }
        
        /// 
        /// Creates an instance of the specified type from the given assembly
        /// 
        /// The assembly containing the type
        /// The full name of the type to instantiate
        /// An instance of the specified type
        public object CreateInstance(Assembly assembly, string typeName)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));
            }
            
            try
            {
                var type = assembly.GetType(typeName);
                
                if (type == null)
                {
                    _logger.LogError($"Type '{typeName}' not found in assembly '{assembly.FullName}'");
                    throw new TypeLoadException($"Type '{typeName}' not found in assembly '{assembly.FullName}'");
                }
                
                _logger.LogInformation($"Creating instance of type: {typeName}");
                return Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating instance of type '{typeName}': {ex.Message}");
                throw;
            }
        }
        
        /// 
        /// Invokes a method on an object with the specified parameters
        /// 
        /// The object instance
        /// Name of the method to invoke
        /// Parameters to pass to the method
        /// The result of the method invocation
        public object InvokeMethod(object instance, string methodName, params object[] parameters)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            
            if (string.IsNullOrWhiteSpace(methodName))
            {
                throw new ArgumentException("Method name cannot be null or empty", nameof(methodName));
            }
            
            try
            {
                var type = instance.GetType();
                var method = type.GetMethod(methodName);
                
                if (method == null)
                {
                    _logger.LogError($"Method '{methodName}' not found on type '{type.FullName}'");
                    throw new MissingMethodException(type.FullName, methodName);
                }
                
                _logger.LogInformation($"Invoking method '{methodName}' on type '{type.FullName}'");
                return method.Invoke(instance, parameters);
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception for clearer error reporting
                _logger.LogError(ex.InnerException, 
                    $"Error invoking method '{methodName}': {ex.InnerException?.Message}");
                throw ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error invoking method '{methodName}': {ex.Message}");
                throw;
            }
        }
        
        /// 
        /// Safely disposes an object by calling its Dispose method if it exists
        /// 
        /// The object to dispose
        public void SafeDispose(object instance)
        {
            if (instance == null)
            {
                return;
            }
            
            try
            {
                var type = instance.GetType();
                var disposeMethod = type.GetMethod("Dispose", Type.EmptyTypes);
                
                if (disposeMethod != null)
                {
                    _logger.LogInformation($"Disposing instance of type '{type.FullName}'");
                    disposeMethod.Invoke(instance, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error disposing object of type '{instance.GetType().FullName}': {ex.Message}");
                // Swallow the exception - we don't want disposal errors to propagate
            }
        }
    }
}