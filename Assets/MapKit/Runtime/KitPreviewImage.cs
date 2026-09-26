namespace Mimicraft.MapKit
{
    /// <summary>
    /// preview.png, a kit map's card picture: its fixed name, its limits, and a reader for the part of it
    /// that is checked before Unity's PNG decoder ever sees the file (plan §14.2).
    ///
    /// The decoder is native code and, like the bundle reader, not something to hand hostile input. The
    /// answer is the same layered one. The packer writes the file itself, re-encoded from a render or from
    /// the author's picture, so a genuine preview is always a plain 8-bit RGB or RGBA PNG no larger than
    /// the limits below. The game checks the size, the hash the manifest names, the signature and the header
    /// against exactly that, and whatever fails never reaches the decoder: the card simply has no picture.
    /// </summary>
    public static class KitPreviewImage
    {
        public const string FileName = "preview.png";

        /// <summary>What the packer writes: 16:9, large enough for a lobby's details panel.</summary>
        public const int Width = 1280;
        public const int Height = 720;

        /// <summary>What the game accepts. Above the packer's own size, so a later kit may write larger
        /// ones without every older game refusing them.</summary>
        public const int MaxWidth = 1920;
        public const int MaxHeight = 1080;
        public const long MaxBytes = 1024 * 1024;

        static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>Reads the PNG signature and the IHDR chunk. True for a non-interlaced 8-bit RGB or RGBA
        /// image within the limits; otherwise false and why.</summary>
        public static bool CheckHeader(byte[] bytes, out int width, out int height, out string why)
        {
            width = 0;
            height = 0;

            // Signature (8) + IHDR length, type and 13 bytes of data (21) + its CRC (4).
            if (bytes == null || bytes.Length < 33)
                return Fail("too short to be a PNG", out why);
            if (bytes.Length > MaxBytes)
                return Fail($"{bytes.Length} bytes, over the {MaxBytes} byte limit", out why);

            for (int i = 0; i < Signature.Length; i++)
            {
                if (bytes[i] != Signature[i])
                    return Fail("not a PNG", out why);
            }

            if (ReadUInt(bytes, 8) != 13 || bytes[12] != (byte)'I' || bytes[13] != (byte)'H'
                || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
                return Fail("the first chunk is not IHDR", out why);

            uint w = ReadUInt(bytes, 16);
            uint h = ReadUInt(bytes, 20);
            if (w == 0 || h == 0 || w > MaxWidth || h > MaxHeight)
                return Fail($"{w}x{h} is outside 1x1 to {MaxWidth}x{MaxHeight}", out why);

            byte depth = bytes[24];
            byte colour = bytes[25];
            if (depth != 8 || (colour != 2 && colour != 6))
                return Fail($"bit depth {depth}, colour type {colour}: only 8-bit RGB or RGBA", out why);

            if (bytes[26] != 0 || bytes[27] != 0 || bytes[28] != 0)
                return Fail("unknown compression or filter method, or interlaced", out why);

            width = (int)w;
            height = (int)h;
            why = "";
            return true;
        }

        static uint ReadUInt(byte[] bytes, int at) =>
            (uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3];

        static bool Fail(string reason, out string why)
        {
            why = reason;
            return false;
        }
    }
}
