namespace SmartCampus.Identity.Api.Domain;

public sealed class User
{
    // Richiesto da EF Core.
    private User()
    {
    }

    public User(Guid id, string username, string displayName, string passwordHash, UserRole role)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo dell'utente è obbligatorio.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Il nome utente è obbligatorio.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Il nome visualizzato è obbligatorio.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("L'hash della password è obbligatorio.", nameof(passwordHash));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Ruolo non valido.");
        }

        Id = id;
        Username = username.Trim();
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
        Role = role;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }
}
