using System.Runtime.InteropServices;

namespace Lexie.Services;

public class NvdaService
{
    private bool _available;
    private bool _checked;

    public bool IsAvailable
    {
        get
        {
            if (!_checked)
            {
                _checked = true;
                try
                {
                    _available = NvdaTestIfRunning() == 0;
                }
                catch (DllNotFoundException)
                {
                    _available = false;
                }
            }
            return _available;
        }
    }

    public void Speak(string text)
    {
        if (!IsAvailable) return;
        try
        {
            NvdaCancelSpeech();
            NvdaSpeakText(text);
        }
        catch
        {
            _available = false;
        }
    }

    public void Cancel()
    {
        if (!IsAvailable) return;
        try { NvdaCancelSpeech(); } catch { }
    }

    [DllImport("nvdaControllerClient64.dll", EntryPoint = "nvdaController_speakText", CharSet = CharSet.Unicode)]
    private static extern int NvdaSpeakText(string text);

    [DllImport("nvdaControllerClient64.dll", EntryPoint = "nvdaController_cancelSpeech")]
    private static extern int NvdaCancelSpeech();

    [DllImport("nvdaControllerClient64.dll", EntryPoint = "nvdaController_testIfRunning")]
    private static extern int NvdaTestIfRunning();
}
