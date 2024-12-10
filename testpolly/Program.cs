using System;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Random = System.Random;

namespace testpolly
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var state = new CircuitBreakerStateProvider();
            var valueTaskCompleted = new ValueTask(Task.CompletedTask);
            
            var builder = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Constant,
                    ShouldHandle = new PredicateBuilder().Handle<InfrastructureException>(e => e.Retry),
                    OnRetry = arguments =>
                    {
                        Console.WriteLine("OnRetry <=============================");
                        return valueTaskCompleted;
                    }
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    BreakDuration = TimeSpan.FromSeconds(10),
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    FailureRatio = 0.5,
                    StateProvider = state,
                    MinimumThroughput = 7,
                    ShouldHandle = new PredicateBuilder().Handle<InfrastructureException>(e => e.CircuitBreaker),
                    OnOpened = arguments =>
                    {
                        Console.WriteLine("OnOpened <=============================");
                        return valueTaskCompleted;  
                    },
                    OnClosed = arguments =>
                    {
                        Console.WriteLine("OnClosed <=============================");
                        return valueTaskCompleted;
                    },
                    OnHalfOpened = arguments =>
                    {
                        Console.WriteLine("OnHalfOpened <=============================");
                        return valueTaskCompleted;
                    }
                });
            var circuitBreaker = builder.Build();
            
            var processor = new Processor();
            Random r = new Random();
            while (true)
            {
                int randomValue = r.Next(1, 3);
                try
                {
                    var result = circuitBreaker.Execute(() => processor.Execute(randomValue));
                    Console.WriteLine($"our result is {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"exception is {ex.Message}; cb policy is {state.CircuitState}");
                }
                finally
                {
                    // Console.WriteLine($"CB state is {PolicyRegistry.InfraCircuitBreakerPolicy}");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}