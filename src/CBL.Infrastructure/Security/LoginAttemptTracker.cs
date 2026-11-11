using CBL.Application.Abstractions;
using CBL.Application.Auth;

namespace CBL.Infrastructure.Security;

/// <summary>
/// Contador de intentos fallidos por correo (exista o no), en memoria del proceso: 5 fallos dentro de la ventana de
/// 15 minutos bloquean ese correo 15 minutos (configurable). Se pierde al reiniciar y no se comparte entre
/// instancias. La memoria está acotada: las entradas caducan con la ventana y hay un máximo de entradas, de modo que
/// correos arbitrarios no hacen crecer el almacén sin control.
/// </summary>
public sealed class LoginAttemptTracker(AuthOptions options, IClock clock, int maxEntries = 10_000) : ILoginAttemptTracker
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly TimeSpan _window = TimeSpan.FromMinutes(options.LockoutMinutes);

    /// <summary>Número de correos con estado (para pruebas).</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool IsLocked(string normalizedEmail, out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            var now = clock.UtcNow;

            if (_entries.TryGetValue(normalizedEmail, out var entry))
            {
                if (IsExpired(entry, now))
                {
                    _entries.Remove(normalizedEmail);
                }
                else if (entry.LockedUntil is { } until && until > now)
                {
                    retryAfter = until - now;
                    return true;
                }
            }

            retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    public void RegisterFailure(string normalizedEmail)
    {
        lock (_gate)
        {
            var now = clock.UtcNow;

            if (!_entries.TryGetValue(normalizedEmail, out var entry) || IsExpired(entry, now))
            {
                MakeRoom(now);
                entry = new Entry { WindowStart = now };
                _entries[normalizedEmail] = entry;
            }

            entry.Failures++;
            entry.LastTouched = now;

            if (entry.Failures >= options.MaxFailedAttempts)
            {
                entry.LockedUntil = now + _window;
            }
        }
    }

    public void Reset(string normalizedEmail)
    {
        lock (_gate)
        {
            _entries.Remove(normalizedEmail);
        }
    }

    private bool IsExpired(Entry entry, DateTime now) =>
        entry.LockedUntil is { } until ? until <= now : now - entry.WindowStart >= _window;

    private void MakeRoom(DateTime now)
    {
        if (_entries.Count < maxEntries)
        {
            return;
        }

        foreach (var key in _entries.Where(e => IsExpired(e.Value, now)).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }

        if (_entries.Count < maxEntries)
        {
            return;
        }

        // Todavía lleno con entradas vigentes: se descartan las menos recientes (al menos una, como mucho el 10 %).
        var toEvict = Math.Max(1, maxEntries / 10);

        foreach (var key in _entries.OrderBy(e => e.Value.LastTouched).Take(toEvict).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }
    }

    private sealed class Entry
    {
        public int Failures { get; set; }

        public DateTime WindowStart { get; init; }

        public DateTime? LockedUntil { get; set; }

        public DateTime LastTouched { get; set; }
    }
}
