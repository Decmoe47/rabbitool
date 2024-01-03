using Autofac.Annotation;

namespace Rabbitool.Common.Provider;

[Component]
public class CancellationTokenProvider : ICancellationTokenProvider
{
    public CancellationTokenProvider()
    {
        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (sender, e) => cts.Cancel();
        Token = cts.Token;
    }

    public CancellationToken Token { get; }
}