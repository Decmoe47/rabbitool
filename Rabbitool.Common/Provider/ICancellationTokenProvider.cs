namespace Rabbitool.Common.Provider;

public interface ICancellationTokenProvider
{
    public CancellationToken Token { get; }
}