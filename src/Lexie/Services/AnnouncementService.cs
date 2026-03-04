using Lexie.Models;

namespace Lexie.Services;

public class AnnouncementService : IDisposable
{
    private readonly SpeechService _speech;
    private readonly NvdaService _nvda;

    public AnnouncementService(SpeechService speech, NvdaService nvda)
    {
        _speech = speech;
        _nvda = nvda;
    }

    public void Announce(string country, string callsign, VoiceMode mode)
    {
        if (mode == VoiceMode.Off) return;

        var spokenCall = FormatCallsignForSpeech(callsign);
        var text = $"{country}, {spokenCall}";

        if (mode == VoiceMode.SelfVoice)
        {
            _speech.Speak(text);
        }
        else if (mode == VoiceMode.ScreenReader)
        {
            if (_nvda.IsAvailable)
                _nvda.Speak(text);
            else
                _speech.Speak(text); // Fall back to SAPI if NVDA not running
        }
    }

    public void Test(VoiceMode mode)
    {
        Announce("Japan", "JA1XYZ", mode);
    }

    /// <summary>
    /// Spell out callsign characters for TTS clarity: "K1ABC" → "K 1 A B C"
    /// </summary>
    private static string FormatCallsignForSpeech(string callsign)
    {
        return string.Join(' ', callsign.ToCharArray());
    }

    public void Dispose()
    {
        _speech.Dispose();
    }
}
