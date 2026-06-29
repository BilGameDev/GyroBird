using System;
using UnityEngine;

public enum ControllerPacketType : byte
{
    LegacyAim = 0,
    LegacyCalibrate = 1,
    LegacyShoot = 2,
    LegacyRestart = 3,
    Aim = 10,
    Calibrate = 11,
    Shoot = 12,
    Restart = 13
}

public readonly struct ControllerPacket
{
    public const int AimPacketSize = 23;
    public const int CommandPacketSize = 7;

    public readonly ControllerPacketType Type;
    public readonly ushort Sequence;
    public readonly float PhoneTime;
    public readonly Quaternion Rotation;

    public ControllerPacket(ControllerPacketType type, ushort sequence, float phoneTime, Quaternion rotation)
    {
        Type = type;
        Sequence = sequence;
        PhoneTime = phoneTime;
        Rotation = rotation;
    }

    public bool IsCommand =>
        Type == ControllerPacketType.Calibrate ||
        Type == ControllerPacketType.Shoot ||
        Type == ControllerPacketType.Restart ||
        Type == ControllerPacketType.LegacyCalibrate ||
        Type == ControllerPacketType.LegacyShoot ||
        Type == ControllerPacketType.LegacyRestart;

    public static int WriteAim(byte[] buffer, ushort sequence, float phoneTime, Quaternion rotation)
    {
        if (buffer == null || buffer.Length < AimPacketSize)
            throw new ArgumentException("Aim packet buffer is too small.", nameof(buffer));

        buffer[0] = (byte)ControllerPacketType.Aim;
        WriteUInt16(buffer, 1, sequence);
        WriteSingle(buffer, 3, phoneTime);
        WriteSingle(buffer, 7, rotation.x);
        WriteSingle(buffer, 11, rotation.y);
        WriteSingle(buffer, 15, rotation.z);
        WriteSingle(buffer, 19, rotation.w);
        return AimPacketSize;
    }

    public static int WriteCommand(byte[] buffer, ControllerPacketType type, ushort sequence, float phoneTime)
    {
        if (buffer == null || buffer.Length < CommandPacketSize)
            throw new ArgumentException("Command packet buffer is too small.", nameof(buffer));

        buffer[0] = (byte)type;
        WriteUInt16(buffer, 1, sequence);
        WriteSingle(buffer, 3, phoneTime);
        return CommandPacketSize;
    }

    public static bool TryParse(byte[] data, out ControllerPacket packet)
    {
        packet = default;
        if (data == null || data.Length == 0)
            return false;

        if (data.Length == 16)
        {
            packet = new ControllerPacket(
                ControllerPacketType.LegacyAim,
                0,
                0f,
                new Quaternion(
                    BitConverter.ToSingle(data, 0),
                    BitConverter.ToSingle(data, 4),
                    BitConverter.ToSingle(data, 8),
                    BitConverter.ToSingle(data, 12)));
            return true;
        }

        var type = (ControllerPacketType)data[0];
        if (type == ControllerPacketType.LegacyAim && data.Length == 17)
        {
            packet = new ControllerPacket(
                ControllerPacketType.LegacyAim,
                0,
                0f,
                new Quaternion(
                    BitConverter.ToSingle(data, 1),
                    BitConverter.ToSingle(data, 5),
                    BitConverter.ToSingle(data, 9),
                    BitConverter.ToSingle(data, 13)));
            return true;
        }

        if ((type == ControllerPacketType.LegacyCalibrate ||
             type == ControllerPacketType.LegacyShoot ||
             type == ControllerPacketType.LegacyRestart) &&
            data.Length == 1)
        {
            packet = new ControllerPacket(type, 0, 0f, Quaternion.identity);
            return true;
        }

        if (type == ControllerPacketType.Aim && data.Length == AimPacketSize)
        {
            packet = new ControllerPacket(
                type,
                ReadUInt16(data, 1),
                BitConverter.ToSingle(data, 3),
                new Quaternion(
                    BitConverter.ToSingle(data, 7),
                    BitConverter.ToSingle(data, 11),
                    BitConverter.ToSingle(data, 15),
                    BitConverter.ToSingle(data, 19)));
            return true;
        }

        if ((type == ControllerPacketType.Calibrate ||
             type == ControllerPacketType.Shoot ||
             type == ControllerPacketType.Restart) &&
            data.Length == CommandPacketSize)
        {
            packet = new ControllerPacket(
                type,
                ReadUInt16(data, 1),
                BitConverter.ToSingle(data, 3),
                Quaternion.identity);
            return true;
        }

        return false;
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Buffer.BlockCopy(bytes, 0, buffer, offset, 2);
    }

    private static ushort ReadUInt16(byte[] buffer, int offset)
    {
        return BitConverter.ToUInt16(buffer, offset);
    }

    private static void WriteSingle(byte[] buffer, int offset, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
    }
}
