using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Reservations;

namespace SmartCampus.Bookings.UnitTests;

// Ogni rifiuto ha un codice di motivo stabile e, quando c'è un limite da rispettare, lo riporta in "limits".
public sealed class RejectionCodesTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    // "Adesso" fisso: lunedì 2 novembre 2026, 10:00 ora di Roma. Così i test danno sempre lo stesso risultato.
    private static readonly DateTimeOffset Now = At(new DateOnly(2026, 11, 2), "10:00");
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Wednesday = new(2026, 11, 4);

    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly Guid StaffUser = Guid.NewGuid();

    // --- Regole sulla richiesta ---

    [Fact]
    public void EndNotAfterStart_HasTheInvalidTimeRangeCode()
    {
        var rejection = ReservationRules.ValidateRequest(Request(Tuesday, "11:00", "11:00"), Now);

        AssertRejection(rejection, 422, RejectionCodes.InvalidTimeRange);
        Assert.Null(rejection.Limits);
    }

    [Fact]
    public void InThePast_HasTheInThePastCode()
    {
        var rejection = ReservationRules.ValidateRequest(Request(new DateOnly(2026, 11, 2), "09:00", "09:30"), Now);

        AssertRejection(rejection, 422, RejectionCodes.InThePast);
    }

    [Fact]
    public void NotOnHalfHour_HasTheSlotBoundaryCodeAndTheSlotLength()
    {
        var rejection = ReservationRules.ValidateRequest(Request(Tuesday, "10:15", "11:00"), Now);

        AssertRejection(rejection, 422, RejectionCodes.SlotBoundary);
        AssertLimit(rejection, "slotMinutes", 30);
    }

    [Fact]
    public void PurposeTooLong_HasTheCodeAndTheMaximumLength()
    {
        var request = new CreateReservationRequest(Guid.NewGuid(), At(Tuesday, "10:00"), At(Tuesday, "11:00"), new string('x', 201));

        var rejection = ReservationRules.ValidateRequest(request, Now);

        AssertRejection(rejection, 422, RejectionCodes.PurposeTooLong);
        AssertLimit(rejection, "maxLength", 200);
    }

    [Fact]
    public void LongerThanFourHours_HasTheDurationCodeAndTheMaximumDuration()
    {
        var rejection = ReservationRules.ValidateRequest(Request(Tuesday, "08:00", "12:30"), Now);

        AssertRejection(rejection, 422, RejectionCodes.DurationExceeded);
        AssertLimit(rejection, "maxDurationMinutes", 240);
    }

    [Fact]
    public void MoreThanFourteenDaysAhead_HasTheAdvanceCodeAndTheMaximumAdvance()
    {
        var rejection = ReservationRules.ValidateRequest(Request(new DateOnly(2026, 11, 16), "10:30", "11:00"), Now);

        AssertRejection(rejection, 422, RejectionCodes.TooFarAhead);
        AssertLimit(rejection, "maxAdvanceDays", 14);
    }

    // --- Regole sullo spazio ---

    [Fact]
    public void NotBookableRoom_HasTheRoomNotBookableCode()
    {
        var room = Room();
        room.IsBookable = false;

        var rejection = ReservationRules.ValidateAgainstRoom(Request(Tuesday, "10:00", "11:00"), room, Rome);

        AssertRejection(rejection, 422, RejectionCodes.RoomNotBookable);
    }

    [Fact]
    public void StartAndEndOnDifferentDays_HasTheDifferentDaysCode()
    {
        var request = new CreateReservationRequest(Guid.NewGuid(), At(Tuesday, "23:00"), At(Wednesday, "01:00"), Purpose: null);

        var rejection = ReservationRules.ValidateAgainstRoom(request, Room(), Rome);

        AssertRejection(rejection, 422, RejectionCodes.DifferentDays);
    }

    [Fact]
    public void ClosedDay_HasTheRoomClosedCode()
    {
        var rejection = ReservationRules.ValidateAgainstRoom(Request(Wednesday, "10:00", "11:00"), Room(), Rome);

        AssertRejection(rejection, 422, RejectionCodes.RoomClosed);
    }

    [Fact]
    public void OutsideOpeningHours_HasTheCodeAndTheOpeningTimes()
    {
        var rejection = ReservationRules.ValidateAgainstRoom(Request(Tuesday, "19:00", "21:00"), Room(), Rome);

        AssertRejection(rejection, 422, RejectionCodes.OutsideOpeningHours);
        AssertLimit(rejection, "opensAt", "08:00");
        AssertLimit(rejection, "closesAt", "20:00");
    }

    // --- Conflitti ---

    [Fact]
    public void RoomConflict_HasTheRoomOccupiedCodeAndTheFreeSlots()
    {
        FreeRange[] freeSlots = [new(At(Tuesday, "12:00"), At(Tuesday, "14:00"))];

        var rejection = ReservationRules.RoomConflict(freeSlots);

        AssertRejection(rejection, 409, RejectionCodes.RoomOccupied);
        Assert.Equal("Lo spazio è già prenotato in questa fascia.", rejection.Detail);
        Assert.Equal(freeSlots, rejection.FreeSlots);
    }

    [Fact]
    public void RoomConflict_WithoutFreeSlots_CarriesAnEmptyListAndNotNull()
    {
        var rejection = ReservationRules.RoomConflict([]);

        Assert.NotNull(rejection.FreeSlots);
        Assert.Empty(rejection.FreeSlots);
    }

    [Fact]
    public void OrganizerConflict_HasThePersonOccupiedCode()
    {
        AssertRejection(ReservationRules.OrganizerConflict(), 409, RejectionCodes.PersonOccupied);
    }

    [Fact]
    public void UserLockTimeout_HasTheBookingInProgressCode()
    {
        AssertRejection(ReservationRules.UserLockTimeout(), 409, RejectionCodes.BookingInProgress);
    }

    // --- Cancellazione ---

    [Fact]
    public void CancellationByAStranger_HasTheForbiddenCode()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Stranger, isStaff: false, hasCheckIn: false, Now);

        AssertRejection(decision.Rejection, 403, RejectionCodes.CancellationForbidden);
    }

    [Fact]
    public void CancellationOfAStartedReservation_HasTheTooLateCode()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now), Organizer, isStaff: false, hasCheckIn: false, Now);

        AssertRejection(decision.Rejection, 422, RejectionCodes.CancellationTooLate);
    }

    [Fact]
    public void CancellationOfAUsedReservation_HasTheAlreadyUsedCode()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Organizer, isStaff: false, hasCheckIn: true, Now);

        AssertRejection(decision.Rejection, 422, RejectionCodes.CancellationAlreadyUsed);
        Assert.Null(decision.Rejection.Limits);
    }

    // --- Check-in ---

    [Fact]
    public void CheckInByAStranger_HasTheForbiddenCode()
    {
        var rejection = ReservationRules.ValidateCheckIn(Booking(Now), Stranger, isMember: false, isStaff: false, existingRecord: null, Now);

        AssertRejection(rejection, 403, RejectionCodes.CheckInForbidden);
    }

    [Fact]
    public void CheckInOfACancelledReservation_HasTheCancelledCode()
    {
        var booking = Booking(Now);
        booking.Status = ReservationStatus.Cancelled;

        var rejection = ReservationRules.ValidateCheckIn(booking, Organizer, isMember: false, isStaff: false, existingRecord: null, Now);

        AssertRejection(rejection, 422, RejectionCodes.CheckInCancelled);
    }

    [Fact]
    public void CheckInOfANoShowReservation_HasTheNoShowCode()
    {
        var rejection = ReservationRules.ValidateCheckIn(Booking(Now), StaffUser, isMember: false, isStaff: true, AttendanceType.NoShow, Now);

        AssertRejection(rejection, 422, RejectionCodes.CheckInNoShow);
    }

    [Fact]
    public void SecondCheckIn_HasTheAlreadyRecordedCode()
    {
        var rejection = ReservationRules.ValidateCheckIn(Booking(Now), Organizer, isMember: false, isStaff: false, AttendanceType.CheckIn, Now);

        AssertRejection(rejection, 422, RejectionCodes.CheckInAlreadyRecorded);
    }

    [Fact]
    public void CheckInOutsideTheWindow_HasTheOutsideWindowCode()
    {
        var rejection = ReservationRules.ValidateCheckIn(Booking(Now.AddDays(1)), Organizer, isMember: false, isStaff: false, existingRecord: null, Now);

        AssertRejection(rejection, 422, RejectionCodes.CheckInOutsideWindow);
        Assert.Null(rejection.Limits);
    }

    [Theory]
    [InlineData(AttendanceType.CheckIn, RejectionCodes.CheckInAlreadyRecorded)]
    [InlineData(AttendanceType.NoShow, RejectionCodes.CheckInNoShow)]
    public void ExistingAttendance_HasTheCodeOfTheRecordFound(AttendanceType recorded, string code)
    {
        AssertRejection(ReservationRules.ExistingAttendance(recorded), 422, code);
    }

    // --- Verifiche e dati di prova ---

    private static void AssertRejection(
        [System.Diagnostics.CodeAnalysis.NotNull] ReservationRejection? rejection,
        int statusCode,
        string code)
    {
        Assert.NotNull(rejection);
        Assert.Equal(statusCode, rejection.StatusCode);
        Assert.Equal(code, rejection.Code);
    }

    private static void AssertLimit(ReservationRejection rejection, string name, object expected)
    {
        Assert.NotNull(rejection.Limits);
        Assert.Equal(expected, rejection.Limits[name]);
    }

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

    private static Reservation Booking(DateTimeOffset start) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = Guid.NewGuid(),
        OrganizerId = Organizer,
        Start = start,
        End = start.AddHours(1),
        Participants = 1,
        Status = ReservationStatus.Confirmed,
        CreatedAt = Now.AddDays(-7)
    };

    private static CreateReservationRequest Request(DateOnly date, string start, string end) =>
        new(Guid.NewGuid(), At(date, start), At(date, end), Purpose: null);

    private static DateTimeOffset At(DateOnly date, string time)
    {
        var local = date.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
