using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;

namespace SmartCampus.Bookings.UnitTests;

public sealed class AvailabilityCalculatorTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly Guid RoomId = Guid.NewGuid();

    // Martedì 3 novembre 2026: ora solare, scarto +01:00.
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    [Fact]
    public void Calculate_WhenRoomIsClosedThatDay_ReturnsClosedWithoutSlots()
    {
        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, openingHours: null, [], Rome);

        Assert.False(result.IsOpen);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public void Calculate_WithoutReservations_ReturnsAllSlotsFreeInCampusTime()
    {
        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, Hours("08:00", "12:00"), [], Rome);

        Assert.True(result.IsOpen);
        Assert.Equal(8, result.Slots.Count);
        Assert.All(result.Slots, slot => Assert.True(slot.IsFree));
        Assert.Equal(At(Tuesday, "08:00"), result.Slots[0].Start);
        Assert.Equal(At(Tuesday, "12:00"), result.Slots[^1].End);
    }

    [Fact]
    public void Calculate_WithConfirmedReservation_MarksOnlyOverlappingSlotsAsOccupied()
    {
        var reservations = new[] { Confirmed("09:00", "10:00") };

        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, Hours("08:00", "12:00"), reservations, Rome);

        Assert.True(SlotAt(result, "08:30").IsFree);   // finisce alle 09:00: adiacente, non sovrapposta
        Assert.False(SlotAt(result, "09:00").IsFree);
        Assert.False(SlotAt(result, "09:30").IsFree);
        Assert.True(SlotAt(result, "10:00").IsFree);   // la prenotazione finisce alle 10:00: [Start, End)
    }

    [Fact]
    public void Calculate_WithReservationCoveringPartOfASlot_MarksTheWholeSlotAsOccupied()
    {
        var reservations = new[] { Confirmed("09:15", "09:45") };

        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, Hours("08:00", "12:00"), reservations, Rome);

        Assert.False(SlotAt(result, "09:00").IsFree);
        Assert.False(SlotAt(result, "09:30").IsFree);
        Assert.True(SlotAt(result, "10:00").IsFree);
    }

    [Fact]
    public void Calculate_IgnoresCancelledReservations()
    {
        var reservations = new[] { Cancelled("09:00", "10:00") };

        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, Hours("08:00", "12:00"), reservations, Rome);

        Assert.All(result.Slots, slot => Assert.True(slot.IsFree));
    }

    [Fact]
    public void Calculate_WhenOpeningHoursAreNotMultipleOfSlot_ReturnsOnlyWholeSlots()
    {
        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, Hours("08:00", "09:45"), [], Rome);

        Assert.Equal(3, result.Slots.Count);   // 08:00, 08:30, 09:00: i 15 minuti finali non bastano
        Assert.Equal(At(Tuesday, "09:30"), result.Slots[^1].End);
    }

    [Fact]
    public void Calculate_WhenRoomClosesNearMidnight_StopsWithoutWrappingAround()
    {
        var result = AvailabilityCalculator.Calculate(RoomId, Tuesday, Hours("22:00", "23:59"), [], Rome);

        Assert.Equal(3, result.Slots.Count);   // 22:00, 22:30, 23:00
        Assert.Equal(At(Tuesday, "23:30"), result.Slots[^1].End);
    }

    [Fact]
    public void Calculate_InSummer_UsesDaylightSavingOffset()
    {
        var summerDay = new DateOnly(2026, 7, 7);

        var result = AvailabilityCalculator.Calculate(RoomId, summerDay, Hours("08:00", "09:00"), [], Rome);

        Assert.Equal(TimeSpan.FromHours(2), result.Slots[0].Start.Offset);   // ora legale: +02:00
    }

    // --- Dati di prova ---

    private static BookableRoomOpeningHours Hours(string opensAt, string closesAt) => new()
    {
        RoomId = RoomId,
        DayOfWeek = DayOfWeek.Tuesday,
        OpensAt = TimeOnly.Parse(opensAt),
        ClosesAt = TimeOnly.Parse(closesAt)
    };

    private static Reservation Confirmed(string start, string end) => Reservation(start, end, ReservationStatus.Confirmed);

    private static Reservation Cancelled(string start, string end) => Reservation(start, end, ReservationStatus.Cancelled);

    private static Reservation Reservation(string start, string end, ReservationStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = RoomId,
        OrganizerId = Guid.NewGuid(),
        Start = At(Tuesday, start),
        End = At(Tuesday, end),
        Participants = 1,
        Status = status
    };

    private static DateTimeOffset At(DateOnly date, string time)
    {
        var local = date.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }

    private static AvailabilitySlot SlotAt(DailyAvailability result, string time) =>
        result.Slots.Single(slot => slot.Start == At(Tuesday, time));
}