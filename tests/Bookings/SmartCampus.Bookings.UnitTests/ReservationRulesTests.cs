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

    // --- Membri del gruppo (US-05) ---

    [Fact]
    public void ValidateMembers_Null_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateMembers(null, Guid.NewGuid()));
    }

    [Fact]
    public void ValidateMembers_Empty_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateMembers([], Guid.NewGuid()));
    }

    [Fact]
    public void ValidateMembers_TenDistinctMembers_IsAccepted()
    {
        Assert.Null(ReservationRules.ValidateMembers(DistinctIds(10), Guid.NewGuid()));
    }

    [Fact]
    public void ValidateMembers_ElevenDistinctMembers_IsRejected()
    {
        var rejection = ReservationRules.ValidateMembers(DistinctIds(11), Guid.NewGuid());

        Assert.Equal(422, rejection?.StatusCode);
        Assert.Equal("Membri non validi", rejection?.Title);
        Assert.Equal("Puoi aggiungere al massimo 10 membri.", rejection?.Detail);
    }

    [Fact]
    public void ValidateMembers_ElevenIdsWithDuplicates_IsRejectedForTheNumberBeforeTheDuplicates()
    {
        var ids = DistinctIds(10).Append(Guid.Empty).ToList();
        ids[^1] = ids[0];

        var rejection = ReservationRules.ValidateMembers(ids, Guid.NewGuid());

        Assert.Equal("Puoi aggiungere al massimo 10 membri.", rejection?.Detail);
    }

    [Fact]
    public void ValidateMembers_OrganizerAmongTheMembers_IsRejected()
    {
        var organizer = Guid.NewGuid();

        var rejection = ReservationRules.ValidateMembers([Guid.NewGuid(), organizer], organizer);

        Assert.Equal(422, rejection?.StatusCode);
        Assert.Equal("Membri non validi", rejection?.Title);
        Assert.Equal("Non puoi aggiungerti come membro: sei già l'organizzatore.", rejection?.Detail);
    }

    [Fact]
    public void ValidateMembers_OrganizerAndDuplicate_ReportsTheOrganizerFirst()
    {
        var organizer = Guid.NewGuid();
        var member = Guid.NewGuid();

        var rejection = ReservationRules.ValidateMembers([member, member, organizer], organizer);

        Assert.Equal("Non puoi aggiungerti come membro: sei già l'organizzatore.", rejection?.Detail);
    }

    [Fact]
    public void ValidateMembers_SameMemberTwice_IsRejected()
    {
        var member = Guid.NewGuid();

        var rejection = ReservationRules.ValidateMembers([member, Guid.NewGuid(), member], Guid.NewGuid());

        Assert.Equal(422, rejection?.StatusCode);
        Assert.Equal("Membri non validi", rejection?.Title);
        Assert.Equal("Ogni studente può essere aggiunto una sola volta.", rejection?.Detail);
    }

    // --- Capienza (INV-5) ---

    [Fact]
    public void ValidateCapacity_ParticipantsEqualToCapacity_IsAccepted()
    {
        var room = Room();
        room.Capacity = 4;

        Assert.Null(ReservationRules.ValidateCapacity(4, room));
    }

    [Fact]
    public void ValidateCapacity_OneParticipantOverCapacity_IsRejected()
    {
        var room = Room();
        room.Capacity = 4;

        var rejection = ReservationRules.ValidateCapacity(5, room);

        Assert.Equal(422, rejection?.StatusCode);
        Assert.Equal("Capienza superata", rejection?.Title);
        Assert.Equal("Lo spazio ospita al massimo 4 persone.", rejection?.Detail);
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
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Organizer, isStaff: false, hasCheckIn: false, Now);

        Assert.Equal(CancellationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void ValidateCancellation_Member_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Member, isStaff: false, hasCheckIn: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_Stranger_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), Stranger, isStaff: false, hasCheckIn: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_StaffOnSomeoneElsesReservation_IsAllowed()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(1)), StaffUser, isStaff: true, hasCheckIn: false, Now);

        Assert.Equal(CancellationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void ValidateCancellation_AlreadyCancelledForOrganizer_ChangesNothing()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1), ReservationStatus.Cancelled), Organizer, isStaff: false, hasCheckIn: false, Now);

        Assert.Equal(CancellationOutcome.AlreadyCancelled, decision.Outcome);
        Assert.Null(decision.Rejection);
    }

    [Fact]
    public void ValidateCancellation_AlreadyCancelledForStranger_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1), ReservationStatus.Cancelled), Stranger, isStaff: false, hasCheckIn: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_PastForStranger_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(-1)), Stranger, isStaff: false, hasCheckIn: false, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_StartEqualToNow_IsRejected()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now), Organizer, isStaff: false, hasCheckIn: false, Now);

        AssertStarted(decision);
    }

    [Fact]
    public void ValidateCancellation_OneTickBeforeStart_IsAllowed()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddTicks(1)), Organizer, isStaff: false, hasCheckIn: false, Now);

        Assert.Equal(CancellationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void ValidateCancellation_Past_IsRejected()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddDays(-1)), Organizer, isStaff: false, hasCheckIn: false, Now);

        AssertStarted(decision);
    }

    [Fact]
    public void ValidateCancellation_StaffOnStartedReservation_IsRejected()
    {
        var decision = ReservationRules.ValidateCancellation(Booking(Now.AddMinutes(-15)), StaffUser, isStaff: true, hasCheckIn: false, Now);

        AssertStarted(decision);
    }

    // --- Cancellazione di una prenotazione usata ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateCancellation_FutureWithCheckIn_IsRejectedAsUsed(bool asStaff)
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1)), asStaff ? StaffUser : Organizer, isStaff: asStaff, hasCheckIn: true, Now);

        AssertUsed(decision);
    }

    [Fact]
    public void ValidateCancellation_StartedWithCheckIn_IsRejectedAsUsedNotAsStarted()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddMinutes(-15)), Organizer, isStaff: false, hasCheckIn: true, Now);

        AssertUsed(decision);
    }

    [Fact]
    public void ValidateCancellation_StrangerOnUsedReservation_IsForbidden()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1)), Stranger, isStaff: false, hasCheckIn: true, Now);

        AssertForbidden(decision);
    }

    [Fact]
    public void ValidateCancellation_AlreadyCancelledWithCheckInForOrganizer_ChangesNothing()
    {
        var decision = ReservationRules.ValidateCancellation(
            Booking(Now.AddDays(1), ReservationStatus.Cancelled), Organizer, isStaff: false, hasCheckIn: true, Now);

        Assert.Equal(CancellationOutcome.AlreadyCancelled, decision.Outcome);
        Assert.Null(decision.Rejection);
    }

    private static void AssertUsed(CancellationDecision decision)
    {
        Assert.Equal(CancellationOutcome.Rejected, decision.Outcome);
        Assert.Equal(422, decision.Rejection?.StatusCode);
        Assert.Equal("Cancellazione non consentita", decision.Rejection?.Title);
        Assert.Equal("Non è possibile cancellare una prenotazione già usata.", decision.Rejection?.Detail);
    }

    // --- Check-in ---
    // Prenotazione di un'ora che inizia esattamente "adesso" (10:00): la finestra va dalle 09:45 alle 10:15.

    private const string OutsideWindow = "Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.";
    private const string CancelledDetail = "Non è possibile fare il check-in di una prenotazione cancellata.";
    private const string NoShowDetail = "La prenotazione è già stata segnata come non usata.";
    private const string AlreadyCheckedInDetail = "Il check-in di questa prenotazione è già stato registrato.";

    public static TheoryData<int> WindowEdgesInMinutes => new() { -15, 0, 15 };

    [Theory]
    [MemberData(nameof(WindowEdgesInMinutes))]
    public void ValidateCheckIn_OrganizerInsideTheWindow_IsAllowed(int minutesFromStart)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), Organizer, isMember: false, isStaff: false, existingRecord: null, Now.AddMinutes(minutesFromStart));

        Assert.Null(rejection);
    }

    [Theory]
    [MemberData(nameof(WindowEdgesInMinutes))]
    public void ValidateCheckIn_MemberInsideTheWindow_IsAllowed(int minutesFromStart)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), Member, isMember: true, isStaff: false, existingRecord: null, Now.AddMinutes(minutesFromStart));

        Assert.Null(rejection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateCheckIn_OneTickBeforeTheWindow_IsRejected(bool asMember)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), asMember ? Member : Organizer, isMember: asMember, isStaff: false, existingRecord: null,
            Now - ReservationRules.CheckInBefore - TimeSpan.FromTicks(1));

        AssertCheckInUnprocessable(rejection, OutsideWindow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateCheckIn_OneTickAfterTheWindow_IsRejected(bool asMember)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), asMember ? Member : Organizer, isMember: asMember, isStaff: false, existingRecord: null,
            Now + ReservationRules.CheckInAfter + TimeSpan.FromTicks(1));

        AssertCheckInUnprocessable(rejection, OutsideWindow);
    }

    [Fact]
    public void ValidateCheckIn_ReservationOfTomorrow_IsRejected()
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now.AddDays(1)), Organizer, isMember: false, isStaff: false, existingRecord: null, Now);

        AssertCheckInUnprocessable(rejection, OutsideWindow);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(40)]
    public void ValidateCheckIn_StaffStrangerOutsideTheWindow_IsAllowed(int minutesFromStart)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), StaffUser, isMember: false, isStaff: true, existingRecord: null, Now.AddMinutes(minutesFromStart));

        Assert.Null(rejection);
    }

    public static TheoryData<ReservationStatus, AttendanceType?> AnyState => new()
    {
        { ReservationStatus.Confirmed, null },
        { ReservationStatus.Cancelled, null },
        { ReservationStatus.Confirmed, AttendanceType.NoShow },
        { ReservationStatus.Confirmed, AttendanceType.CheckIn }
    };

    [Theory]
    [MemberData(nameof(AnyState))]
    public void ValidateCheckIn_Stranger_IsForbiddenWhateverTheState(ReservationStatus status, AttendanceType? existingRecord)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now, status), Stranger, isMember: false, isStaff: false, existingRecord, Now);

        Assert.Equal(403, rejection?.StatusCode);
        Assert.Equal("Check-in non consentito", rejection?.Title);
        Assert.Equal("Non puoi fare il check-in di questa prenotazione.", rejection?.Detail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateCheckIn_CancelledInsideTheWindow_IsRejected(bool asStaff)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now, ReservationStatus.Cancelled), asStaff ? StaffUser : Organizer, isMember: false, isStaff: asStaff,
            existingRecord: null, Now);

        AssertCheckInUnprocessable(rejection, CancelledDetail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateCheckIn_NoShowInsideTheWindow_IsRejected(bool asStaff)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), asStaff ? StaffUser : Organizer, isMember: false, isStaff: asStaff,
            AttendanceType.NoShow, Now.AddMinutes(10));

        AssertCheckInUnprocessable(rejection, NoShowDetail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateCheckIn_AlreadyCheckedInInsideTheWindow_IsRejected(bool asStaff)
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), asStaff ? StaffUser : Member, isMember: !asStaff, isStaff: asStaff,
            AttendanceType.CheckIn, Now.AddMinutes(2));

        AssertCheckInUnprocessable(rejection, AlreadyCheckedInDetail);
    }

    [Fact]
    public void ValidateCheckIn_CancelledOutsideTheWindow_IsRejectedAsCancelled()
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now.AddDays(1), ReservationStatus.Cancelled), Organizer, isMember: false, isStaff: false,
            existingRecord: null, Now);

        AssertCheckInUnprocessable(rejection, CancelledDetail);
    }

    [Fact]
    public void ValidateCheckIn_AlreadyCheckedInOutsideTheWindow_IsRejectedAsAlreadyCheckedIn()
    {
        var rejection = ReservationRules.ValidateCheckIn(
            Booking(Now), Organizer, isMember: false, isStaff: false, AttendanceType.CheckIn, Now.AddMinutes(30));

        AssertCheckInUnprocessable(rejection, AlreadyCheckedInDetail);
    }

    private static void AssertCheckInUnprocessable(ReservationRejection? rejection, string detail)
    {
        Assert.Equal(422, rejection?.StatusCode);
        Assert.Equal("Check-in non consentito", rejection?.Title);
        Assert.Equal(detail, rejection?.Detail);
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

    private static List<Guid> DistinctIds(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static CreateReservationRequest Request(DateOnly date, string start, string end) =>
        new(Guid.NewGuid(), At(date, start), At(date, end), Purpose: null);

    private static DateTimeOffset At(DateOnly date, string time)
    {
        var local = date.ToDateTime(TimeOnly.Parse(time));
        return new DateTimeOffset(local, Rome.GetUtcOffset(local));
    }
}
