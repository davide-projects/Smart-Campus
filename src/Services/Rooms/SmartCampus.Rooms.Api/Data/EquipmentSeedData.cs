namespace SmartCampus.Rooms.Data;

internal static class EquipmentSeedData
{
    private static readonly Guid ClassroomA1Id = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ClassroomA2Id = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid ClassroomA3Id = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    private static readonly Guid ComputerLab1Id = Guid.Parse("a1000000-0000-0000-0000-000000000005");
    private static readonly Guid ComputerLab2Id = Guid.Parse("a1000000-0000-0000-0000-000000000006");
    private static readonly Guid ClassroomB1Id = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid ClassroomB2Id = Guid.Parse("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid ElectronicsLabId = Guid.Parse("b1000000-0000-0000-0000-000000000003");

    private static readonly Guid ProjectorId = Guid.Parse("e1000000-0000-0000-0000-000000000001");
    private static readonly Guid PersonalComputerId = Guid.Parse("e1000000-0000-0000-0000-000000000002");
    private static readonly Guid WhiteboardId = Guid.Parse("e1000000-0000-0000-0000-000000000003");
    private static readonly Guid Printer3DId = Guid.Parse("e1000000-0000-0000-0000-000000000004");
    private static readonly Guid OscilloscopeId = Guid.Parse("e1000000-0000-0000-0000-000000000005");

    public static IReadOnlyList<EquipmentRecord> Catalog { get; } =
    [
        new(ProjectorId, "Proiettore"),
        new(PersonalComputerId, "PC"),
        new(WhiteboardId, "Lavagna"),
        new(Printer3DId, "Stampante 3D"),
        new(OscilloscopeId, "Oscilloscopio")
    ];

    public static IReadOnlyList<RoomEquipmentAssignment> Assignments { get; } =
    [
        new(ClassroomA1Id, ProjectorId),
        new(ClassroomA1Id, WhiteboardId),
        new(ClassroomA2Id, WhiteboardId),
        new(ClassroomA3Id, ProjectorId),
        new(ClassroomA3Id, WhiteboardId),
        new(ComputerLab1Id, PersonalComputerId),
        new(ComputerLab1Id, ProjectorId),
        new(ComputerLab2Id, PersonalComputerId),
        new(ComputerLab2Id, Printer3DId),
        new(ClassroomB1Id, ProjectorId),
        new(ClassroomB1Id, WhiteboardId),
        new(ClassroomB2Id, WhiteboardId),
        new(ElectronicsLabId, OscilloscopeId),
        new(ElectronicsLabId, PersonalComputerId)
    ];
}
