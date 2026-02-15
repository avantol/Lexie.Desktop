namespace Lexie.Models;

public record WsjtxMessage(WsjtxMessageType Type, string? ClientId, uint Schema);

public record HeartbeatMessage(string? ClientId, uint Schema, uint MaxSchema, string? Version, string? Revision)
    : WsjtxMessage(WsjtxMessageType.Heartbeat, ClientId, Schema);

public record StatusMessage(string? ClientId, uint Schema, ulong DialFrequency, string? Mode,
    string? DxCall, string? Report, string? TxMode, bool TxEnabled, bool Transmitting, bool Decoding)
    : WsjtxMessage(WsjtxMessageType.Status, ClientId, Schema);

public record DecodeMessage(string? ClientId, uint Schema, bool IsNew, uint TimeMs,
    int Snr, double DeltaTime, uint DeltaFrequency, string? Mode, string? Message, bool LowConfidence)
    : WsjtxMessage(WsjtxMessageType.Decode, ClientId, Schema);

public record QsoLoggedMessage(string? ClientId, uint Schema, string? DxCall, string? DxGrid, string? Mode)
    : WsjtxMessage(WsjtxMessageType.QsoLogged, ClientId, Schema);

public record WsprDecodeMessage(string? ClientId, uint Schema, bool IsNew, uint TimeMs,
    int Snr, double DeltaTime, ulong Frequency, int Drift, string? Callsign, string? Grid, int Power)
    : WsjtxMessage(WsjtxMessageType.WsprDecode, ClientId, Schema);

public record CloseMessage(string? ClientId, uint Schema)
    : WsjtxMessage(WsjtxMessageType.Close, ClientId, Schema);
