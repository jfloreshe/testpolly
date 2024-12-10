using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;
using Polly.Wrap;

namespace testpolly
{
    public class ExceptionDetectionService
    {
        public void HandleError(Exception ex)
        {
            HandleKafkaErrors(ex);
            HandleInternalErrors(ex);
            throw ex;
        }

        private void HandleInternalErrors(Exception ex)
        {
            long clusterCode = 2000000000; //error de codigo tira por exception no controlado
            if(ex is InternalException iEx)
            {
                //validar si es un error de negocio
                clusterCode = 1000000000;
                throw new InternalException(
                    $"Internal error => {ex.Message}; {ex.InnerException?.Message}",
                    GetMsCodeError() + clusterCode + iEx.IdError, 
                    ex);
            }
            
            //errores de codigo
            throw new InternalException(
                $"Internal error => {ex.Message}; {ex.InnerException?.Message}",
                GetMsCodeError() + clusterCode + 1, //1 error generico
                ex);
        }
        private void HandleKafkaErrors(Exception ex)
        {
            //validar si es un error de kafka
            if (!ex.Message.Contains("kafka")) return;
            //al asegurarnos debemos verificar si nuestro tipo de error es candidato para retry y circuitbreaker
            long clusterCode = 5000000000;
            long specificCode = 1000000;
            throw new InfrastructureException(
                $"Kafka error => {ex.Message}; {ex.InnerException?.Message}",
                GetMsCodeError() + clusterCode + specificCode,
                ex)
                {
                    CircuitBreaker = true,
                    Retry = true
                };


            // SqlException sqlEx and  => 
            //                         
            // DbException dbEx => dbEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) throw new InternalException(),
            // _ => throw new Exception(ex);

        }

        private long GetMsCodeError()
        {
            return 1000000000000;
        }
        
        //
        // private bool IsSimulationException(Exception ex, out InternalException? iE)
        // {
        //     iE = null;
        //     var result = ex switch
        //     {
        //         InternalException iEx => iEx.IdError == Utilitarios.ERRORCONTABILIDAD.Simulado,
        //         _ => false
        //     };
        //     if (result) iE = (InternalException)ex;
        //     return result;
        // }
    }
    public class InternalException : ApplicationException
    {
        public long IdError { get; private set; }
        public InternalException(string message, long idError, Exception? innerException = null) : base(message, innerException)
        {
            IdError = idError;
        }
    }
    public class InfrastructureException : ApplicationException
    {
        public long IdError { get; private set; }
        public bool CircuitBreaker { get; set; } = false;
        public bool Retry { get; set; } = false;
        public InfrastructureException(string message, long idError, Exception? innerException = null) : base(message, innerException)
        {
            IdError = idError;
        }
    }
    public class Processor
    {
        private readonly ExceptionDetectionService _exceptionDetectionService = new ExceptionDetectionService();
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
                _exceptionDetectionService.HandleError(ex);
                throw;
            }
        }

        private int ExecuteDbOperation(int a)
        {
            if (a == 1)
            {
                throw new InternalException("bad luck, validation failed", 100); //100 code user defined
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
    
    public static class PolicyRegistry
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        }).CreateLogger("CircuitBreaker");
    
        public static RetryPolicy NoRetryPolicy = Policy
            .Handle<InternalException>()
            .WaitAndRetry(0, _ => TimeSpan.FromSeconds(1),
                onRetry: (exception, delay, context) => Console.WriteLine($"{"Retry",-10}{delay,-10:ss\\.fff}: {exception.GetType().Name}"));
        public static CircuitBreakerPolicy InfraCircuitBreakerPolicy { get; } =
            Policy
                .Handle<InfrastructureException>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(10),
                    onBreak: (exception, duration) =>
                        Logger.LogWarning(
                            $"Circuit OPEN for {duration.TotalSeconds} seconds due to: {exception.Message}"),
                    onReset: () =>
                        Logger.LogInformation("Circuit CLOSED - Reset"),
                    onHalfOpen: () =>
                        Logger.LogInformation("Circuit HALF-OPEN - Testing recovery")
                );
    }
}