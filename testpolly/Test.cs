// using System;
// using System.Threading.Tasks;
// using Polly;
//
// namespace testpolly
// {
//     public class ResultCircuitBreaker<T>
//     {
//         public static AsyncPolicy<T> Create(
//             Func<T, bool> isFailure,
//             int exceptionsBeforeBreaking = 3,
//             TimeSpan? breakDuration = null)
//         {
//             return Policy<T>
//                 .Handle<Exception>()
//                 .OrResult(isFailure)
//                 .CircuitBreakerAsync(
//                     exceptionsBeforeBreaking,
//                     breakDuration ?? TimeSpan.FromSeconds(10),
//                     onBreak: (_, duration) =>
//                     {
//                         // Optional logging
//                         Console.WriteLine($"Circuit broken for {duration}");
//                         ;
//                     },
//                     onReset: () => { Console.WriteLine("Circuit reset"); }
//                 );
//         }
//
//         public static async Task<T> ExecuteAsync(
//             Func<Task<T>> operation,
//             Func<T, bool> isFailure,
//             T fallbackValue = default)
//         {
//             var policy = Create(isFailure);
//
//             return await policy.ExecuteAsync(async () =>
//             {
//                 var result = await operation();
//                 return isFailure(result) ? throw new Exception("Result considered a failure") : result;
//             }).ConfigureAwait(false);
//         }
//     }
//
// // Example Usage
//     public class ExampleService
//     {
//         public async Task<Result<string>> SomeMethod()
//         {
//             var result = await ResultCircuitBreaker<Result<string>>.ExecuteAsync(
//                 async () => await SomeRiskyOperation(),
//                 result => !result.IsSuccess
//             );
//
//             return result;
//         }
//
//         private async Task<Result<string>> SomeRiskyOperation()
//         {
//             // Simulated operation
//             await Task.Delay(100);
//             return Result<string>.Success("Operation Result");
//         }
//     }
//
// // Simple Result type for demonstration
//     public class Result<T>
//     {
//         public T Value { get; }
//         public bool IsSuccess { get; }
//         public string Error { get; }
//
//         private Result(T value, bool isSuccess, string error = null)
//         {
//             Value = value;
//             IsSuccess = isSuccess;
//             Error = error;
//         }
//
//         public static Result<T> Success(T value) =>
//             new Result<T>(value, true);
//
//         public static Result<T> Failure(string error) =>
//             new Result<T>(default, false, error);
//     }
// }