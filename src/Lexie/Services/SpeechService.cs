using System.Speech.Synthesis;

namespace Lexie.Services;

public class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly Queue<string> _queue = new();
    private bool _speaking;

    public SpeechService()
    {
        _synth.Rate = 2;
        _synth.SpeakCompleted += (_, _) =>
        {
            _speaking = false;
            DrainQueue();
        };
    }

    public void Speak(string text)
    {
        _queue.Enqueue(text);
        if (!_speaking) DrainQueue();
    }

    public void Cancel()
    {
        _queue.Clear();
        _synth.SpeakAsyncCancelAll();
        _speaking = false;
    }

    private void DrainQueue()
    {
        if (_queue.Count == 0) return;
        _speaking = true;
        var text = _queue.Dequeue();
        _synth.SpeakAsync(text);
    }

    public void Dispose()
    {
        _synth.Dispose();
    }
}
