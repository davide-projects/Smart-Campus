namespace SmartCampus.Gateway.Auth;

/// <summary>Ruoli presenti nel claim JWT.</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Staff = "Staff";
}

/// <summary>
/// Nomi delle policy usate in <c>AuthorizationPolicy</c> nelle route YARP (appsettings.json).
/// <c>Default</c> (utente autenticato) e <c>Anonymous</c> (nessun controllo) sono nomi riservati da YARP.
/// </summary>
public static class AccessPolicies
{
    public const string Staff = "Staff";
}
