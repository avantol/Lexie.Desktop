using System.Buffers.Binary;
using System.Text;
using Lexie.Models;

namespace Lexie.Services;

public class WsjtxParser
{
    private const uint Magic = 0xADBCCBDA;

    private byte[] _buf = [];
    private int _off;

    private int Remaining => _buf.Length - _off;

    private uint ReadUInt32()
    {
        var val = BinaryPrimitives.ReadUInt32BigEndian(_buf.AsSpan(_off, 4));
        _off += 4;
        return val;
    }

    private int ReadInt32()
    {
        var val = BinaryPrimitives.ReadInt32BigEndian(_buf.AsSpan(_off, 4));
        _off += 4;
        return val;
    }

    private ulong ReadUInt64()
    {
        var val = BinaryPrimitives.ReadUInt64BigEndian(_buf.AsSpan(_off, 8));
        _off += 8;
        return val;
    }

    private double ReadDouble()
    {
        var val = BinaryPrimitives.ReadDoubleBigEndian(_buf.AsSpan(_off, 8));
        _off += 8;
        return val;
    }

    private byte ReadByte()
    {
        return _buf[_off++];
    }

    private bool ReadBool() => ReadByte() != 0;

    private string? ReadUtf8String()
    {
        var length = ReadUInt32();
        if (length == 0xFFFFFFFF) return null;
        if (length == 0) return string.Empty;
        var str = Encoding.UTF8.GetString(_buf, _off, (int)length);
        _off += (int)length;
        return str;
    }

    public WsjtxMessage? Parse(byte[] data)
    {
        if (data.Length < 12) return null;

        _buf = data;
        _off = 0;

        try
        {
            var magic = ReadUInt32();
            if (magic != Magic) return null;

            var schema = ReadUInt32();
            var typeVal = ReadUInt32();
            var clientId = ReadUtf8String();

            var type = (WsjtxMessageType)typeVal;

            return type switch
            {
                WsjtxMessageType.Heartbeat => ParseHeartbeat(clientId, schema),
                WsjtxMessageType.Status => ParseStatus(clientId, schema),
                WsjtxMessageType.Decode => ParseDecode(clientId, schema),
                WsjtxMessageType.QsoLogged => ParseQsoLogged(clientId, schema),
                WsjtxMessageType.WsprDecode => ParseWsprDecode(clientId, schema),
                WsjtxMessageType.Close => new CloseMessage(clientId, schema),
                _ => new WsjtxMessage(type, clientId, schema),
            };
        }
        catch
        {
            return null;
        }
    }

    private HeartbeatMessage ParseHeartbeat(string? clientId, uint schema)
    {
        var maxSchema = ReadUInt32();
        var version = ReadUtf8String();
        var revision = ReadUtf8String();
        return new HeartbeatMessage(clientId, schema, maxSchema, version, revision);
    }

    private StatusMessage ParseStatus(string? clientId, uint schema)
    {
        var dialFreq = ReadUInt64();
        var mode = ReadUtf8String();
        var dxCall = ReadUtf8String();
        var report = ReadUtf8String();
        var txMode = ReadUtf8String();
        var txEnabled = ReadBool();
        var transmitting = ReadBool();
        var decoding = ReadBool();

        // Optional fields — skip safely if present
        // (rxDF, txDF, deCall, deGrid, dxGrid, txWatchdog, subMode, fastMode,
        //  specialOp, freqTol, trPeriod, configName, txMessage)
        // We only need the fields above, so we don't parse the rest.

        return new StatusMessage(clientId, schema, dialFreq, mode, dxCall, report,
            txMode, txEnabled, transmitting, decoding);
    }

    private DecodeMessage ParseDecode(string? clientId, uint schema)
    {
        var isNew = ReadBool();
        var time = ReadUInt32();
        var snr = ReadInt32();
        var deltaTime = ReadDouble();
        var deltaFreq = ReadUInt32();
        var mode = ReadUtf8String();
        var message = ReadUtf8String();
        var lowConfidence = ReadBool();

        return new DecodeMessage(clientId, schema, isNew, time, snr, deltaTime,
            deltaFreq, mode, message, lowConfidence);
    }

    private QsoLoggedMessage ParseQsoLogged(string? clientId, uint schema)
    {
        var dateTimeOff = ReadUInt64();
        var dxCall = ReadUtf8String();
        var dxGrid = ReadUtf8String();
        var txFreq = ReadUInt64();
        var mode = ReadUtf8String();
        // Skip remaining optional fields (reportSent, reportReceived, etc.)

        return new QsoLoggedMessage(clientId, schema, dxCall, dxGrid, mode);
    }

    private WsprDecodeMessage ParseWsprDecode(string? clientId, uint schema)
    {
        var isNew = ReadBool();
        var time = ReadUInt32();
        var snr = ReadInt32();
        var deltaTime = ReadDouble();
        var frequency = ReadUInt64();
        var drift = ReadInt32();
        var callsign = ReadUtf8String();
        var grid = ReadUtf8String();
        var power = ReadInt32();

        return new WsprDecodeMessage(clientId, schema, isNew, time, snr, deltaTime,
            frequency, drift, callsign, grid, power);
    }
}
