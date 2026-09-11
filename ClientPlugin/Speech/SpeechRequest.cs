using System.Threading;

namespace ClientPlugin.Speech;

// One transcript waiting for the game thread's verdict. Handy is answered only
// after the game thread claimed and processed the request, or after the
// listener gave up waiting - never both, so the text is pasted exactly once.
internal sealed class SpeechRequest
{
    private const int Pending = 0;
    private const int Claimed = 1;
    private const int Abandoned = 2;

    public readonly string Text;

    private readonly ManualResetEventSlim completed = new ManualResetEventSlim();
    private int state = Pending;

    public bool Handled { get; private set; }

    public SpeechRequest(string text)
    {
        Text = text;
    }

    // Game thread: true if it now owns the request
    public bool TryClaim() => Interlocked.CompareExchange(ref state, Claimed, Pending) == Pending;

    // Game thread, after a successful TryClaim
    public void Complete(bool handled)
    {
        Handled = handled;
        completed.Set();
    }

    // Listener thread: the verdict, or false when the game thread did not pick
    // the request up in time (its TryClaim will then fail and it skips it)
    public bool WaitForVerdict(int timeoutMs)
    {
        if (completed.Wait(timeoutMs))
            return Handled;

        if (Interlocked.CompareExchange(ref state, Abandoned, Pending) == Pending)
            return false;

        // Claimed meanwhile: the game thread is on it, the verdict is imminent
        return completed.Wait(timeoutMs) && Handled;
    }
}
