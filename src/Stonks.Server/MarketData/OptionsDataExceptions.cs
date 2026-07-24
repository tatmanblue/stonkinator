namespace Stonks.Server.MarketData;

public sealed class OptionsSubscriptionRequiredException : Exception
{
    public OptionsSubscriptionRequiredException(string message) : base(message) { }
}

public sealed class OptionsDataUnauthorizedException : Exception
{
    public OptionsDataUnauthorizedException(string message) : base(message) { }
}

public sealed class OptionsDataUnavailableException : Exception
{
    public OptionsDataUnavailableException(string message) : base(message) { }
}
