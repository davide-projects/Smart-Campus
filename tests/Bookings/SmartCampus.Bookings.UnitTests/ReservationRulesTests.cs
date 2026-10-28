using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
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

    // --- Cancellazione ---

    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly Guid StaffUser = Guid.NewGuid();

    [Fact]
    public void ValidateCancellation_OrganizerOnFutureReservation_IsAllowed()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Organizer, isStaff: false, Now);

        Assert.Equal(CancellationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void ValidateCancellation_Member_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Member, isStaff: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_Stranger_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Stranger, isStaff: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_StaffOnSomeoneElsesReservation_IsAllowed()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), StaffUser, isStaff: true, Now);

        Assert.Equal(CancellationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void ValidateCancellation_AlreadyCancelledForOrganizer_ChangesNothing()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1), ReservationStatus.Cancelled), Organizer, isStaff: false, Now);

        Assert.Equal(CancellationOutcome.AlreadyCancelled, decision.Outcome);
        Assert.Null(decision.Rejection);
    }

    [Fact]
    public void ValidateCancellation_AlreadyCancelledForStranger_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1), ReservationStatus.Cancelled), Stranger, isStaff: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_PastForStranger_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(-1)), Stranger, isStaff: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_StartEqualToNow_IsRejected()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now), Organizer, isStaff: false, Now);

        AssertStarted(decision);
    }

    [Fact]
    public void ValidateCancellation_OneTickBeforeStart_IsAllowed()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddTicks(1)), Organizer, isStaff: false, Now);

        Assert.Equal(CancellationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void ValidateCancellation_Past_IsRejected()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(-1)), Organizer, isStaff: false, Now);

        AssertStarted(decision);
    }

    [Fact]
    public void ValidateCancellation_StaffOnStartedReservation_IsRejected()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddMinutes(-15)), StaffUser, isStaff: true, Now);

        AssertStarted(decision);
    }

    private static void AssertForbidden(CancellationDecision decision)
    {
        Assert.Equal(CancellationOutcome.Rejected, decision.Outcome);
        Assert.Equal(403, decision.Rejection?.StatusCode);
        Assert.Equal("Cancellazione non consentita", decision.Rejection?.Title);
        Assert.Equal("Non puoi cancellare questa prenotazione.", decision.Rejection?.Detail);
    }

    private static void AssertStarted(CancellationDecision decision)
    {
        Assert.Equal(CancellationOutcome.Rejected, decision.Outcome);
        Assert.Equal(422, decision.Rejection?.StatusCode);
        Assert.Equal("Cancellazione non consentita", decision.Rejection?.Title);
        Assert.Equal("Non è possibile cancellare una prenotazione già iniziata o passata.", decision.Rejection?.Detail);
    }

    // Prenotazione di un'ora di Organizer, di gruppo con Member.
    private static Reservation Booking(DateTimeOffset start, ReservationStatus status = ReservationStatus.Confirmed)
    {
        var id = Guid.NewGuid();
        return new Reservation
        {
            Id = id,
            RoomId = Guid.NewGuid(),
            OrganizerId = Organizer,
            Start = start,
            End = start.AddHours(1),
            Participants = 2,
            Status = status,
            CreatedAt = Now.AddDays(-7),
            CancelledAt = status == ReservationStatus.Cancelled ? Now.AddHours(-1) : null,
            Members = [new ReservationMember { ReservationId = id, UserId = Member }]
        };
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
