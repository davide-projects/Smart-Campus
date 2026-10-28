namespace SmartCampus.Bookings.IntegrationTests;

// Orologio dei test di integrazione: segue l'ora reale finché un test non fissa "adesso" con Set.
// I test che lo fissano lo riportano all'ora reale con Reset, così le altre classi non ne risentono.
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset? _now;

    public void Set(DateTimeOffset now) => _now = now;

    public void Reset() => _now = null;

    public override DateTimeOffset GetUtcNow() => _now?.ToUniversalTime() ?? base.GetUtcNow();
}
