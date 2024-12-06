using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Wrap;

namespace testpolly
{
    public abstract class ExceptionDetectionService
    {
        public abstract void HandleBeforeError(Exception ex)
        {
            
        }

        public abstract void HandleAfterError(Exception ex)
        {
            
        }
        
        public void HandleError(Exception ex)
        {
            
        }
        private void DbErrors(Exception ex)
        {
            switch (ex)
            {
                case SqlException sqlEx:
                    var newEx = new InternalException($"{nameof(SqlException)} => {sqlEx.Message}", 4000, ex);
                    if (sqlEx.Number == -2 || sqlEx.Number == -1 || sqlEx.Number == 11 || sqlEx.Number == 121 ||
                        sqlEx.Number == 258 || sqlEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        newEx.CircuitBreaker = true;
                        newEx.Retry = true;
                    }
                        
                    
                
            }
            
                // SqlException sqlEx and  => 
                //                         
                // DbException dbEx => dbEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) throw new InternalException(),
                // _ => throw new Exception(ex);
            
        }

        private bool IsSimulationException(Exception ex, out InternalException? iE)
        {
            iE = null;
            var result = ex switch
            {
                InternalException iEx => iEx.IdError == Utilitarios.ERRORCONTABILIDAD.Simulado,
                _ => false
            };
            if (result) iE = (InternalException)ex;
            return result;
        }
    }
    public class InternalException : ApplicationException
    {
        public int IdError { get; private set; }
        public bool CircuitBreaker { get; set; } = false;
        public bool Retry { get; set; } = false;
        public InternalException(string message, int idError, Exception? innerException = null) : base(message, innerException)
        {
            IdError = idError;
        }
    }
    public class Processor
    {
        public int Execute(int a)
        {
            Console.WriteLine($"Executing {a}");

            try
            {
                var db = ExecuteDbOperation(a);

                var kafka = ExecuteKafkaOperation(a);

                return a;
            }
            catch (Exception ex)
            {
                return 3;
            }
        }

        private int ExecuteDbOperation(int a)
        {
            if (a == 1)
            {
                throw new InternalException("bad luck", 100);
            }

            return a;
        }
        private int ExecuteKafkaOperation(int a)
        {
            if (a == 2)
            {
                throw new Exception("kafka");
            }

            return a;
        }
    }
    
    // public static class PolicyRegistry
    // {
    //     private static readonly ILogger Logger = LoggerFactory.Create(builder =>
    //     {
    //         builder.AddConsole();
    //     }).CreateLogger("CircuitBreaker");
    //
    //     public static CircuitBreakerPolicy DatabaseCircuitBreaker { get; } =
    //         Policy
    //             .HandleResult<Result<T>>(e =>
    //             {
    //                 Console.WriteLine($"db handle called : {e.Message.Contains("db")}");
    //                 return e.Message.Contains("db");
    //             })
    //             .CircuitBreaker(
    //                 exceptionsAllowedBeforeBreaking: 3,
    //                 durationOfBreak: TimeSpan.FromSeconds(10),
    //                 onBreak: (exception, duration) =>
    //                     Logger.LogWarning(
    //                         $"Circuit OPEN for {duration.TotalSeconds} seconds due to: {exception.Message}"),
    //                 onReset: () =>
    //                     Logger.LogInformation("Circuit CLOSED - Reset"),
    //                 onHalfOpen: () =>
    //                     Logger.LogInformation("Circuit HALF-OPEN - Testing recovery")
    //             );
    // }
}