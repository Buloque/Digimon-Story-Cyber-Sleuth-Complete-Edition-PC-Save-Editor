using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DigimonStorySaveEditor.SaveFormats
{
    internal sealed class PcSaveDocument
    {
        public const int PcSize = 843568;
        public const int EditorSize = PcSize + 32;
        // Switch offsets, BEFORE any removals. See README for the layout source.
        private static readonly int[] Gaps = {
            0x4BA0C, 0x4BA9C, 0x4BACC, 0x4BB0C,
            0xAD1AC, 0xAD23C, 0xAD26C, 0xAD2AC
        };
        public const int PartyBase = 0x3CA9C;
        public const int StoryOffset = 0x617A0;
        public const int PartySize = 11;
        public const int SlotSize = 0x240;
        private byte[] diskImage;
        public string FilePath { get; private set; }
        public byte[] Data { get; private set; }

        public static PcSaveDocument Open(string path)
        {
            if (!Regex.IsMatch(Path.GetFileName(path), @"^[0-9]{4}\.bin$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new InvalidDataException("Abra um save numerado, como 0000.bin. slot_0000.bin e system_data.bin não são suportados.");
            var info = new FileInfo(path);
            if (info.Length != PcSize)
                throw new InvalidDataException("O save de PC deve ter 843.568 bytes. Saves Switch, arquivos truncados e outros formatos não são suportados.");
            byte[] encrypted = File.ReadAllBytes(path);
            byte[] expanded = Expand(PcSaveCipher.Decrypt(encrypted));
            Validate(expanded);
            return new PcSaveDocument { FilePath = Path.GetFullPath(path), diskImage = encrypted, Data = expanded };
        }

        private static void Validate(byte[] data)
        {
            // Do not validate character names as ASCII: unstarted HM uses Japanese text.
            for (int story = 0; story < 2; story++)
            {
                int delta = story * StoryOffset;
                if (BitConverter.ToUInt32(data, 0x4B9B8 + delta) > 9999999 ||
                    BitConverter.ToUInt32(data, 0x4B9C0 + delta) > 20)
                    throw new InvalidDataException("O conteúdo descriptografado não corresponde ao layout de save de PC suportado.");
                for (int slot = 0; slot < PartySize; slot++)
                {
                    int start = PartyBase + delta + slot * SlotSize;
                    uint species = BitConverter.ToUInt32(data, start + 0x10);
                    if (data[start] != 0 && (species == 0 || (species > 999 && species != 65535)))
                        throw new InvalidDataException("O save contém um ID de Digimon fora do layout suportado.");
                }
            }
        }

        public static byte[] Expand(byte[] pc)
        {
            if (pc.Length != PcSize) throw new InvalidDataException("Tamanho de save de PC inválido.");
            byte[] result = new byte[EditorSize];
            int source = 0, target = 0;
            foreach (int gap in Gaps)
            {
                int count = gap - target;
                Buffer.BlockCopy(pc, source, result, target, count);
                source += count;
                target = gap + 4;
            }
            Buffer.BlockCopy(pc, source, result, target, pc.Length - source);
            return result;
        }

        public static byte[] Compact(byte[] editor)
        {
            if (editor.Length != EditorSize) throw new InvalidDataException("Tamanho de buffer do editor inválido.");
            byte[] result = new byte[PcSize];
            int source = 0, target = 0;
            foreach (int gap in Gaps)
            {
                int count = gap - source;
                Buffer.BlockCopy(editor, source, result, target, count);
                target += count;
                source = gap + 4;
            }
            Buffer.BlockCopy(editor, source, result, target, editor.Length - source);
            return result;
        }

        public void Save(byte[] editor)
        {
            Validate(editor);
            byte[] output = PcSaveCipher.Encrypt(Compact(editor));
            CheckUnchangedOnDisk();
            string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(output, 0, output.Length);
                    file.Flush(true);
                }
                CheckUnchangedOnDisk();
                // Create the first backup exclusively; never overwrite a user's existing backup.
                if (!File.Exists(FilePath + ".bak"))
                    File.Copy(FilePath, FilePath + ".bak", false);
                File.Replace(temporary, FilePath, null);
                diskImage = output;
                Data = (byte[])editor.Clone();
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void CheckUnchangedOnDisk()
        {
            if (!File.ReadAllBytes(FilePath).SequenceEqual(diskImage))
                throw new IOException("O save mudou no disco desde a abertura. Feche o jogo e reabra o arquivo antes de salvar.");
        }
    }
}
