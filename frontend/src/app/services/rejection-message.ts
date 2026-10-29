import { ApiProblem } from './api-problem';

// Messaggio in italiano per ogni motivo di rifiuto di una prenotazione: dipende dalla proprietà «code»
// del ProblemDetails di Bookings (elenco in docs/handoff-architettura.md, 6.2). I limiti da rispettare
// (proprietà «limits») entrano nel testo, così il messaggio dice sempre il valore in vigore.
// Un codice sconosciuto, o a cui manca un limite, ripiega sul testo scritto dal servizio (detail);
// senza nemmeno quello il risultato è null e la pagina usa il messaggio generico.
export function rejectionText(
  problem: Pick<ApiProblem, 'code' | 'limits' | 'detail'>,
): string | null {
  return textOfCode(problem.code, problem.limits) ?? textOfDetail(problem.detail);
}

function textOfDetail(detail: string | null): string | null {
  return detail !== null && detail.trim() !== '' ? detail : null;
}

// Le parole sono quelle del servizio, anche per i motivi delle prenotazioni di gruppo (US-05).
function textOfCode(code: string | null, limits: Record<string, unknown> | null): string | null {
  switch (code) {
    case 'room-occupied':
      return 'Lo spazio è già prenotato in questa fascia.';
    case 'person-occupied':
      return 'Hai già una prenotazione in questa fascia oraria.';
    case 'members-occupied':
      return 'Uno o più membri hanno già una prenotazione in questa fascia.';
    case 'member-booking-in-progress':
      return "Un membro del gruppo ha un'altra prenotazione in corso, riprova tra qualche secondo.";
    case 'booking-in-progress':
      return "Hai un'altra prenotazione in corso, riprova tra qualche secondo.";
    case 'room-closed':
      return 'Lo spazio è chiuso in questo giorno.';
    case 'organizer-as-member':
      return "Non puoi aggiungerti come membro: sei già l'organizzatore.";
    case 'duplicate-members':
      return 'Ogni studente può essere aggiunto una sola volta.';
    case 'members-not-students':
      return 'Puoi aggiungere come membri solo studenti registrati.';
    case 'members-check-unavailable':
      return 'Non è possibile verificare i membri in questo momento. Riprova tra poco.';
    case 'room-not-bookable':
      return 'Questo spazio non è prenotabile.';
    case 'invalid-time-range':
      return "L'orario di fine deve essere successivo a quello di inizio.";
    case 'in-the-past':
      return 'Non è possibile prenotare nel passato.';
    case 'different-days':
      return 'La prenotazione deve iniziare e finire nello stesso giorno.';
    case 'outside-opening-hours': {
      const opensAt = limits?.['opensAt'];
      const closesAt = limits?.['closesAt'];
      return typeof opensAt === 'string' && typeof closesAt === 'string'
        ? `Lo spazio è aperto dalle ${opensAt} alle ${closesAt}.`
        : null;
    }
    case 'duration-exceeded': {
      const minutes = numberOf(limits, 'maxDurationMinutes');
      return minutes === null
        ? null
        : `Una prenotazione può durare al massimo ${durationLabel(minutes)}.`;
    }
    case 'too-far-ahead': {
      const days = numberOf(limits, 'maxAdvanceDays');
      return days === null
        ? null
        : `Si può prenotare al massimo ${days === 1 ? '1 giorno' : `${days} giorni`} prima.`;
    }
    case 'slot-boundary': {
      const minutes = numberOf(limits, 'slotMinutes');
      if (minutes === null) {
        return null;
      }
      return minutes === 30
        ? "Le prenotazioni iniziano e finiscono all'ora esatta o alla mezz'ora."
        : `Le prenotazioni iniziano e finiscono ogni ${minutes} minuti.`;
    }
    case 'capacity-exceeded': {
      const capacity = numberOf(limits, 'maxParticipants');
      return capacity === null
        ? null
        : `Lo spazio ospita al massimo ${capacity === 1 ? '1 persona' : `${capacity} persone`}.`;
    }
    case 'too-many-members': {
      const maxMembers = numberOf(limits, 'maxMembers');
      return maxMembers === null
        ? null
        : `Puoi aggiungere al massimo ${maxMembers === 1 ? '1 membro' : `${maxMembers} membri`}.`;
    }
    case 'purpose-too-long': {
      const maxLength = numberOf(limits, 'maxLength');
      return maxLength === null
        ? null
        : `Il motivo può contenere al massimo ${maxLength} caratteri.`;
    }
    default:
      return null;
  }
}

function numberOf(limits: Record<string, unknown> | null, name: string): number | null {
  const value = limits?.[name];
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

// 240 → «4 ore», 60 → «1 ora», 90 → «90 minuti».
function durationLabel(minutes: number): string {
  if (minutes % 60 === 0) {
    const hours = minutes / 60;
    return hours === 1 ? '1 ora' : `${hours} ore`;
  }
  return `${minutes} minuti`;
}
