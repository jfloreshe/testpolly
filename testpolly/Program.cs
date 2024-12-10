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
            ResilenceStrategyBuilder strategy = new ResilenceStrategyBuilder();
            var resilenceStrategy = strategy.CreateNewPipeline()
                .AddCircuitBreaker(ResilenceStrategyBuilder.cbKafkaOptions)
                .AddCircuitBreaker(ResilenceStrategyBuilder.cbDbOptions)
                .Build();
            
            var processor = new Processor();
            Random r = new Random();
            while (true)
            {
                int randomValue = r.Next(1, 3);
                try
                {
                    var result = resilenceStrategy.Execute(() => processor.Execute(randomValue));
                    Console.WriteLine($"our result is {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"exception is {ex.Message}; cbDB policy is {ResilenceStrategyBuilder.cbDbState.CircuitState}");
                    Console.WriteLine($"exception is {ex.Message}; cbKAFKA policy is {ResilenceStrategyBuilder.cbKakfaState.CircuitState}");
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