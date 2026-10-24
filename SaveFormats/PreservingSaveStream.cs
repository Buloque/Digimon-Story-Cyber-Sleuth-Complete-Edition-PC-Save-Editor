using System;
using System.IO;

namespace DigimonStorySaveEditor.SaveFormats
{
    // Compare each serialized field with its initial UI representation, not the
    // raw file. Values rounded for display and unknown bytes survive unchanged.
    internal sealed class PreservingSaveStream : MemoryStream
    {
        private readonly byte[] projection;
        public PreservingSaveStream(byte[] original, byte[] projection)
            : base((byte[])original.Clone(), true) { this.projection = projection; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position < 0 || Position + count > Length) throw new InvalidDataException("A edição excede o tamanho do save.");
            bool unchanged = projection != null;
            if (unchanged)
                for (int i = 0; i < count; i++)
                    if (buffer[offset + i] != projection[(int)Position + i]) { unchanged = false; break; }
            if (unchanged) Position += count;
            else base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value) { Write(new[] { value }, 0, 1); }
    }
}
