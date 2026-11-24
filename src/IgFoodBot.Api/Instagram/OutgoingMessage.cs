namespace IgFoodBot.Api.Instagram;

public abstract record OutgoingMessage
{
    public abstract string ToPlainText();
}

public sealed record TextMessage(string Text) : OutgoingMessage
{
    public override string ToPlainText() => Text;
}

public sealed record LinkCardMessage(string Title, string Subtitle, string ButtonTitle, string Url) : OutgoingMessage
{
    public override string ToPlainText() => $"{Title}\n{Subtitle}\n{Url}".Trim();
}
