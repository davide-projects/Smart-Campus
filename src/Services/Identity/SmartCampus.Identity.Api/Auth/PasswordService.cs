using Microsoft.AspNetCore.Identity;

namespace SmartCampus.Identity.Api.Auth;

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
    // Esegue una verifica fittizia, così il tempo di risposta per un utente inesistente
    // è simile a quello di un utente esistente con password errata
    void SimulateVerification(string password);
}

// Hash delle password con l'hasher standard di ASP.NET Core
public sealed class PasswordService : IPasswordService
{
    // L'hasher standard ignora l'utente: serve solo a soddisfare la firma del metodo.
    private static readonly object Subject = new();

    private readonly PasswordHasher<object> _hasher = new();
    private readonly Lazy<string> _dummyHash;

    public PasswordService()
    {
        _dummyHash = new Lazy<string>(() => Hash("password-fittizia-non-valida"));
    }

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(Subject, passwordHash, password) != PasswordVerificationResult.Failed;

    public void SimulateVerification(string password) =>
        _hasher.VerifyHashedPassword(Subject, _dummyHash.Value, password);
}
