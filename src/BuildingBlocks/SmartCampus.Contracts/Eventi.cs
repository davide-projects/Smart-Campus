namespace SmartCampus.Contracts;

// Solo i messaggi scambiati tra servizi tramite broker.
// NON inserire qui entità di dominio o classi EF: ogni servizio possiede il proprio modello.

// Pubblicati da Spazi, consumati da Prenotazioni (copia locale SpazioPrenotabile).
public record SpazioCreato(Guid SpazioId, string Nome, int Capienza, bool Prenotabile);
public record SpazioAggiornato(Guid SpazioId, string Nome, int Capienza, bool Prenotabile);
public record SpazioDisattivato(Guid SpazioId);

// Pubblicati da Prenotazioni, consumati da Presenze.
public record PrenotazioneConfermata(Guid PrenotazioneId, Guid SpazioId, Guid UtenteId, DateTimeOffset Inizio, DateTimeOffset Fine);
public record PrenotazioneCancellata(Guid PrenotazioneId, Guid SpazioId, DateTimeOffset CancellataIl);
