using System;
using System.IO;
using SubliminalSarcasm.InputSystem;

namespace SubliminalSarcasm.NetworkData
{
    public static class PacketSerializer
    {
        // We know exactly how big our struct is:
        // FrameID (4) + ButtonMask (2) + 4x Shorts (8) = 14 Bytes
        public const int PACKET_SIZE = 14;

        public static byte[] Serialize(NormalizedInputFrame frame)
        {
            using (MemoryStream m = new MemoryStream(PACKET_SIZE))
            {
                using (BinaryWriter w = new BinaryWriter(m))
                {
                    w.Write(frame.FrameID);
                    w.Write(frame.ButtonMask);
                    w.Write(frame.MoveX);
                    w.Write(frame.MoveY);
                    w.Write(frame.LookX);
                    w.Write(frame.LookY);
                }
                return m.ToArray();
            }
        }

        public static NormalizedInputFrame Deserialize(byte[] data)
        {
            // Safety check
            if (data.Length < PACKET_SIZE) return new NormalizedInputFrame();

            NormalizedInputFrame frame = new NormalizedInputFrame();
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader r = new BinaryReader(m))
                {
                    frame.FrameID = r.ReadUInt32();
                    frame.ButtonMask = r.ReadUInt16();
                    frame.MoveX = r.ReadInt16();
                    frame.MoveY = r.ReadInt16();
                    frame.LookX = r.ReadInt16();
                    frame.LookY = r.ReadInt16();
                }
            }
            return frame;
        }
    }
}