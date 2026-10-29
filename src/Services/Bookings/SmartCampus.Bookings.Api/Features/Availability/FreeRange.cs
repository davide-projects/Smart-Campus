namespace SmartCampus.Bookings.Api.Features.Availability;

// Un intervallo libero di uno spazio: le fasce libere consecutive, unite in un solo intervallo.
public sealed record FreeRange(DateTimeOffset Start, DateTimeOffset End);
