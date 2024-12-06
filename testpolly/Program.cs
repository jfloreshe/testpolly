using System;
using System.Threading.Tasks;

namespace testpolly
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var processor = new Processor();
            Random r = new Random();
            while (true)
            {
                int randomValue = r.Next(1, 3);
                try
                {
                    var result = await ResultCircuitBreaker<Result<int>>.ExecuteAsync(
                        async () => await Task.FromResult(processor.Execute(randomValue)),
                        result => !result.IsSuccess
                    );
                    //
                    // return result;
                    // var data = PolicyRegistry.policyKafkaDb.Execute(() => processor.Execute(randomValue));
                    // Console.WriteLine($"our result is {data}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"exception is {ex.Message}");
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}