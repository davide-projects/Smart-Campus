namespace SmartCampus.Bookings.IntegrationTests;

// Tutte le classi di test di Bookings condividono UNA sola fixture (un solo SQL Server in container):
// meno memoria usata e avvio più veloce. Le classi della collezione vengono eseguite una dopo l'altra.
[CollectionDefinition(Name)]
public sealed class BookingsApiCollection : ICollectionFixture<BookingsApiFixture>
{
    public const string Name = "Bookings API";
}
