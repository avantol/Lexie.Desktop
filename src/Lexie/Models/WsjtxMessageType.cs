namespace Lexie.Models;

public enum WsjtxMessageType
{
    Heartbeat = 0,
    Status = 1,
    Decode = 2,
    Clear = 3,
    Reply = 4,
    QsoLogged = 5,
    Close = 6,
    Replay = 7,
    HaltTx = 8,
    FreeText = 9,
    WsprDecode = 10,
    Location = 11,
    LoggedAdif = 12,
    HighlightCallsign = 13,
    SwitchConfiguration = 14,
    Configure = 15,
}
