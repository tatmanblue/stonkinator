namespace Stonks.Server.Badges;

public interface IBadgeExtractor
{
    IReadOnlyList<string> Extract(string analysisText);
}
