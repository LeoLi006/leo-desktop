namespace LeoLauncher;

sealed class DragTransition
{
    int active, pending;
    long since;
    public bool IsPending => pending != 0;
    public void Reset() { active = pending = 0; since = 0; }
    public int Request(int direction, long now)
    {
        if (active == 0) { active = direction; return active; }
        if (direction == active) { pending = 0; return active; }
        if (pending != direction) { pending = direction; since = now; }
        return Advance(now);
    }
    public int Advance(long now)
    {
        if (pending == 0) return active;
        if (now - since < 140) return active;
        if (now - since < 240) return 0;
        active = pending; pending = 0; return active;
    }
}
