namespace Roguelike.Rules
{
    /// <summary>
    /// Holds the newest press made while the character was locked. When the lock ends the press is
    /// taken: it fires if it is no older than the buffer window, and is dropped otherwise.
    /// </summary>
    public sealed class InputBuffer<T>
    {
        T _item;
        float _storedAt;

        public bool HasItem { get; private set; }

        /// <summary>The newest press replaces an older one.</summary>
        public void Store(T item, float now)
        {
            _item = item;
            _storedAt = now;
            HasItem = true;
        }

        /// <summary>Empties the buffer. Returns true, with the press, only if it is still fresh.</summary>
        public bool TryTake(float now, float window, out T item)
        {
            item = default;
            if (!HasItem) return false;
            HasItem = false;
            if (now - _storedAt > window) return false;
            item = _item;
            return true;
        }

        public void Clear() => HasItem = false;
    }
}
