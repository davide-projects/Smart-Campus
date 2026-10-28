using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.UnitTests;

public sealed class ReservationRulesTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // "Adesso" fisso: lunedì 2 novembre 2026, 10:00 ora di Roma. Così i test danno sempre lo stesso risultato.
    private static readonly DateTimeOffset Now = At(new DateOnly(2026, 11, 2), "10:00");
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    // --- Gruppo 1: regole sulla richiesta ---

    [Fact]
    public void ValidateRequest_ValidRequest_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateRequest(Request(Tuesday, "10:00", "11:00"), Now));
    }

    [Fact]
    public void ValidateRequest_EndNotAfterStart_IsRejected()
    {
        var rejection = ReservationRules.ValidateRequest(Request(Tuesday, "11:00", "11:00"), Now);

        Assert.Equal(422, rejection?.StatusCode);
    }

    [Fact]
    public void ValidateRequest_InThePast_IsRejected()
    {
        var rejection = ReservationRules.ValidateRequest(Request(new DateOnly(2026, 11, 2), "09:00", "09:30"), Now);

        Assert.Equal("Orario nel passato", rejection?.Title);
    }

    [Fact]
    public void ValidateRequest_NotOnHalfHour_IsRejected()
    {
        var rejection = ReservationRules.ValidateRequest(Request(Tuesday, "10:15", "11:00"), Now);

        Assert.Equal(422, rejection?.StatusCode);
    }

    [Fact]
    public void ValidateRequest_ExactlyFourHours_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateRequest(Request(Tuesday, "08:00", "12:00"), Now));
    }

    [Fact]
    public void ValidateRequest_LongerThanFourHours_IsRejected()
    {
        var rejection = ReservationRules.ValidateRequest(Request(Tuesday, "08:00", "12:30"), Now);

        Assert.Equal("Durata eccessiva", rejection?.Title);
    }

    [Fact]
    public void ValidateRequest_ExactlyFourteenDaysAhead_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateRequest(Request(new DateOnly(2026, 11, 16), "10:00", "11:00"), Now));
    }

    [Fact]
    public void ValidateRequest_MoreThanFourteenDaysAhead_IsRejected()
    {
        var rejection = ReservationRules.ValidateRequest(Request(new DateOnly(2026, 11, 16), "10:30", "11:00"), Now);

        Assert.Equal("Troppo in anticipo", rejection?.Title);
    }

    // --- Gruppo 2: regole sullo spazio ---

    [Fact]
    public void ValidateAgainstRoom_InsideOpeningHours_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateAgainstRoom(Request(Tuesday, "08:00", "10:00"), Room(), Rome));
    }

    [Fact]
    public void ValidateAgainstRoom_EndingExactlyAtClosing_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateAgainstRoom(Request(Tuesday, "18:00", "20:00"), Room(), Rome));
    }

    [Fact]
    public void ValidateAgainstRoom_OutsideOpeningHours_IsRejectedWithTheOpeningTimes()
    {
        var rejection = ReservationRules.ValidateAgainstRoom(Request(Tuesday, "19:00", "21:00"), Room(), Rome);

        Assert.Equal(422, rejection?.StatusCode);
        Assert.Equal("Lo spazio è aperto dalle 08:00 alle 20:00.", rejection?.Detail);
    }

    [Fact]
    public void ValidateAgainstRoom_OnClosedDay_IsRejected()
    {
        var wednesday = new DateOnly(2026, 11, 4);

        var rejection = ReservationRules.ValidateAgainstRoom(Request(wednesday, "10:00", "11:00"), Room(), Rome);

        Assert.Equal("Lo spazio è chiuso in questo giorno.", rejection?.Detail);
    }

    [Fact]
    public void ValidateAgainstRoom_DeactivatedRoom_IsRejected()
    {
        var room = Room();
        room.IsActive = false;

        var rejection = ReservationRules.ValidateAgainstRoom(Request(Tuesday, "10:00", "11:00"), room, Rome);

        Assert.Equal("Spazio non prenotabile", rejection?.Title);
    }

    [Fact]
    public void ValidateAgainstRoom_NotBookableRoom_IsRejected()
    {
        var room = Room();
        room.IsBookable = false;

        var rejection = ReservationRules.ValidateAgainstRoom(Request(Tuesday, "10:00", "11:00"), room, Rome);

        Assert.Equal("Spazio non prenotabile", rejection?.Title);
    }

    // --- Dati di prova ---

    // Aperto solo il martedì, 08:00-20:00.
    private static BookableRoom Room()
    {
        var roomId = Guid.NewGuid();
        var room = new BookableRoom { RoomId = roomId, Name = "Aula A1", Capacity = 30, IsBookable = true, IsActive = true };
        room.OpeningHours.Add(new BookableRoomOpeningHours
        {
            RoomId = roomId,
            DayOfWeek = DayOfWeek.Tuesday,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(20, 0)
        });
        return room;
    }

    private static CreateReservationRequest Request(DateOnly date, string start, string end) =>
        new(Guid.NewGuid(), At(date, start), At(date, end), Purpose: null);

    private static DateTimeOffset At(DateOnly date, string time)
    {
        var local = date.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
