# Lykke.HttpClientGenerator
Helps to generate client proxy for an http api using the Refit library. It adds cache and retries using Polly to the generated proxies.

Nuget: https://www.nuget.org/packages/Lykke.HttpClientGenerator/

# Quickstart guide
1. Create a nuget package containing interface of your service with the data types used in it.
2. In a consumer application add reference to the interface package and to this one.
3. To create an instance of the client proxy use this code:
```csharp
var generator = HttpClientGenerator.BuildForUrl(serviceUrl).Create();
var client = generator.Generate<IApi>();
```
This example creates a client with a linear retry strategy with default params (6 times with 5 sec pauses) 
and caching with parameters specified in method attributes.
To add caching to a method of an interface:
```csharp
[ClientCaching(Minutes = 1, Seconds = 30)]
[Get("/api/get-foo"]
Task<string> GetFoo();
```
Only get methods caching is supported via attributes by default. 

Usually you should create a single instance of each proxy in your application and reuse it everywhere. 
It is completely thread-safe. Creating multiple instances can cause some problems.

# Customizations
HttpClientGenerator.BuildForUrl() returns a `HttpClientGeneratorBuilder` object with fluent customisation api.

By default it adds:
- an autogenerated User-Agent header (from application metadata);
- retries with a linear policy: 6 times with a delay of 5 seconds;
- the caching ability (to enable it add the `ClientCachingAttribute` to the method and specify the time).

Api-key header is not added by default. To add it - use the `HttpClientGeneratorBuilder.WithApiKey()` method.

## Calls wrappers
Calls wrappers (or handlers) are executed around the api interface methods:
```csharp
public interface ICallsWrapper
{
    Task<object> HandleMethodCall(MethodInfo targetMethod, object[] args, Func<Task<object>> innerHandler);
}
```

Example wrapper implementation:
```csharp
public async Task<object> HandleMethodCall(MethodInfo targetMethod, object[] args, Func<Task<object>> innerHandler)
{
    Console.WriteLine($"{method.Name} before");
    var result = await inner();
    Console.WriteLine($"{method.Name} after");
    return result;
};
```
If the function do not call `inner()` - it effectively restricts the invocation of actual wrapped method.
One can add try-catch here, execute inner code in a separate thread, add logging, caching or retries, etc.
The wrappers are executed in the order they are added. 
Calling `inner()` in the first handler means calling the second one.
Calling `inner()` in the last one - means calling the actial method.

HttpClientGenerator has only one default wrapper: `CachingCallsWrapper`.

To add your custom one use the `HttpClientGeneratorBuilder.WithAdditionalCallsWrapper()` method.

### Caching customization
Constructor of `CachingCallsWrapper` accepts a `ICachingStrategy` object. There is one default strategy: `AttributeBasedCachingStrategy`. Implement the `ICachingStrategy` interface and pass it to the `HttpClientGeneratorBuilder.WithCachingStrategy()` method to specify your own behavior.

To disable caching call the `HttpClientGeneratorBuilder.WithoutCaching()` method.

## Http DelegatingHandlers
Other, more performant, way of adding custom logic to requests is to implement the `DelegatingHandler` abstract class.

DelegatingHandlers do not wrap the Refit logic of transforming a method call to an http request, instead they wrap the actual http request logic.

You can add several DelegatingHandlers. They are executed in the order they are added. The last one will call an HttpClientHandler which actually makes the request.

Example: 
```csharp
var generator = HttpClientGenerator.BuildForUrl(serviceUrl)
.WithAdditionalDelegatingHandler(new MySpecialHandler())
.Create();
```
```csharp
public class MySpecialHandler : DelegatingHandler
{    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("before");
        await base.SendAsync(request, cancellationToken);
        Console.WriteLine("after");
    }
}
```
There are some default handlers in the library:
- `ApiKeyHeaderHttpClientHandler`
- `UserAgentHeaderHttpClientHandler`
- `RetryingHttpClientHandler`

### Retries customization
The `RetryingHttpClientHandler` constructor accepts an IRetryStrategy:
```csharp
public interface IRetryStrategy
{
    TimeSpan GetRetrySleepDuration(int retryAttempt, string url);
    int RetryAttemptsCount { get; }
}
```
There are 2 default configurable implementations:
- `LinearRetryStrategy`
- `ExponentialRetryStrategy`

You can customize the strategy used via the `HttpClientGeneratorBuilder.WithRetriesStrategy()` method.

To disable retries call the `HttpClientGeneratorBuilder.WithoutRetries()` method.

Implementing a more sophisticated retries logic may require creating a separate DelegatingHandler implementation.
