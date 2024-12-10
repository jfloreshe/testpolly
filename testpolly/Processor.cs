using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
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
            HandleDbErrors(ex);
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
            throw new KafkaInfrastructureException(
                $"Kafka error => {ex.Message}; {ex.InnerException?.Message}",
                GetMsCodeError() + clusterCode + specificCode,
                ex)
                {
                    CircuitBreaker = true,
                    Retry = true
                };
        }
        
        private void HandleDbErrors(Exception ex)
        {
            //validar si es un error de kafka
            if (!ex.Message.Contains("db")) return;
            //al asegurarnos debemos verificar si nuestro tipo de error es candidato para retry y circuitbreaker
            long clusterCode = 4000000000;
            long specificCode = 1000000;
            throw new DbInfrastructureException(
                $"db error => {ex.Message}; {ex.InnerException?.Message}",
                GetMsCodeError() + clusterCode + specificCode,
                ex)
            {
                CircuitBreaker = true,
                Retry = true
            };
        }

        private long GetMsCodeError()
        {
            return 1000000000000;
        }
    }
    public class InternalException : ApplicationException
    {
        public long IdError { get; private set; }
        public InternalException(string message, long idError, Exception? innerException = null) : base(message, innerException)
        {
            IdError = idError;
        }
    }

    public class DbInfrastructureException : InfrastructureException
    {
        public DbInfrastructureException(string message, long idError, Exception? innerException = null): base(message, idError, innerException){}
    }
    public class KafkaInfrastructureException : InfrastructureException
    {
        public KafkaInfrastructureException(string message, long idError, Exception? innerException = null): base(message, idError, innerException){}
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
                // throw new InternalException("bad luck, validation failed", 100); //100 code user defined
                throw new Exception("db");
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
    //     public static RetryPolicy NoRetryPolicy = Policy
    //         .Handle<InternalException>()
    //         .WaitAndRetry(0, _ => TimeSpan.FromSeconds(1),
    //             onRetry: (exception, delay, context) => Console.WriteLine($"{"Retry",-10}{delay,-10:ss\\.fff}: {exception.GetType().Name}"));
    //     public static CircuitBreakerPolicy InfraCircuitBreakerPolicy { get; } =
    //         Policy
    //             .Handle<InfrastructureException>()
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

    public class ResilenceStrategyBuilder
    {
        private ResiliencePipelineBuilder? builder;
        private static readonly ValueTask _valueTaskCompleted = new ValueTask(Task.CompletedTask);
        public static CircuitBreakerStateProvider cbDbState = new CircuitBreakerStateProvider();
        public static CircuitBreakerStrategyOptions cbDbOptions = new CircuitBreakerStrategyOptions
        {
            BreakDuration = TimeSpan.FromSeconds(10),
            SamplingDuration = TimeSpan.FromSeconds(10),
            FailureRatio = 0.5,
            StateProvider = cbDbState,
            MinimumThroughput = 7,
            ShouldHandle = new PredicateBuilder().Handle<DbInfrastructureException>(e => e.CircuitBreaker),
            OnOpened = arguments =>
            {
                Console.WriteLine("CBDB OnOpened <=============================");
                return _valueTaskCompleted;  
            },
            OnClosed = arguments =>
            {
                Console.WriteLine("CBDB OnClosed <=============================");
                return _valueTaskCompleted;
            },
            OnHalfOpened = arguments =>
            {
                Console.WriteLine("CBDB OnHalfOpened <=============================");
                return _valueTaskCompleted;
            }
        };
        
        public static CircuitBreakerStateProvider cbKakfaState = new CircuitBreakerStateProvider();
        public static CircuitBreakerStrategyOptions cbKafkaOptions = new CircuitBreakerStrategyOptions
        {
            BreakDuration = TimeSpan.FromSeconds(10),
            SamplingDuration = TimeSpan.FromSeconds(10),
            FailureRatio = 0.5,
            StateProvider = cbKakfaState,
            MinimumThroughput = 7,
            ShouldHandle = new PredicateBuilder().Handle<KafkaInfrastructureException>(e => e.CircuitBreaker),
            OnOpened = arguments =>
            {
                Console.WriteLine("CBKafka OnOpened <=============================");
                return _valueTaskCompleted;  
            },
            OnClosed = arguments =>
            {
                Console.WriteLine("CBKafka OnClosed <=============================");
                return _valueTaskCompleted;
            },
            OnHalfOpened = arguments =>
            {
                Console.WriteLine("CBKafka OnHalfOpened <=============================");
                return _valueTaskCompleted;
            }
        };

        public ResiliencePipelineBuilder CreateNewPipeline()
        {
            builder = new ResiliencePipelineBuilder();
            return builder;
        }
        public ResiliencePipelineBuilder AddCircuiBreak(CircuitBreakerStrategyOptions cbOptions)
        {
            if (builder == null)
                throw new Exception("Se necesita inicializar el Pipeline, use CreateNewPipeline primero");

            return builder.AddCircuitBreaker(cbOptions);
        }
        
        public ResiliencePipelineBuilder AddRetry(RetryStrategyOptions retryOptions)
        {
            if (builder == null)
                throw new Exception("Se necesita inicializar el Pipeline, use CreateNewPipeline primero");

            return builder.AddRetry(retryOptions);
        }
        
        

        public ResiliencePipeline Build()
        {
            if (builder == null)
                throw new Exception("Se necesita inicializar el Pipeline, use CreateNewPipeline primero");
            
            return builder.Build();
        }
    }
}