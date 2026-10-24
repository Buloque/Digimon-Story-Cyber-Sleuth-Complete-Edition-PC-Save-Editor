using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using DigimonStorySaveEditor.SaveFormats;

namespace DigimonStorySaveEditor
{
    public partial class MainMenu
    {
        private PcSaveDocument document;
        private byte[] editorData;
        private byte[] baselineProjection;
        private bool loadingSave;
        private readonly List<PartySlotEditor> reserveEditors = new List<PartySlotEditor>();

        private void InitializePcEditor()
        {
            var species = new List<object>();
            foreach (string row in digimonIDs)
                if (row.Length > 6 && !row.StartsWith("00000:") && !row.StartsWith("65535:"))
                    species.Add(row.Substring(6).TrimEnd('\r'));
            for (int story = 0; story < 2; story++)
                for (int slot = 1; slot <= 3; slot++)
                {
                    var combo = (ComboBox)Controls.Find("comboBox" + (story == 0 ? "CS" : "HM") + "DigimonPartySlot" + slot + "ID", true)[0];
                    combo.Items.Clear();
                    combo.Items.AddRange(species.ToArray());
                }
            comboBoxCSCharacterStatsCharacterGender.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCSCharacterStatsCharacterGender.Items.AddRange(new object[] { "Male", "Female" });
            var parties = new[] { tabControlCSDigimonPartySlots, tabControlHMDigimonPartySlots };
            for (int story = 0; story < parties.Length; story++)
            {
                parties[story].Multiline = true;
                parties[story].Dock = DockStyle.Fill;
                for (int slot = 0; slot < 3; slot++) parties[story].TabPages[slot].Text = "Slot " + (slot + 1) + " (Battle)";
                for (int slot = 3; slot < PcSaveDocument.PartySize; slot++)
                {
                    var editor = new PartySlotEditor(story, slot, (campaign, position) => { ChangePartySlot(campaign, position); });
                    var page = new TabPage("Slot " + (slot + 1) + " (Reserve)");
                    page.Controls.Add(editor);
                    parties[story].TabPages.Add(page);
                    reserveEditors.Add(editor);
                }
            }
            tabMain.Enabled = false;
            saveToolStripMenuItem.Enabled = false;
        }

        internal void OpenPcSave(string path)
        {
            var next = PcSaveDocument.Open(path);
            var previous = document;
            var previousData = editorData;
            try
            {
                document = next;
                editorData = (byte[])next.Data.Clone();
                ReloadEditor();
                savegame = next.FilePath;
            }
            catch
            {
                document = previous;
                editorData = previousData;
                if (document != null) ReloadEditor();
                else { tabMain.Enabled = false; saveToolStripMenuItem.Enabled = false; }
                throw;
            }
        }

        internal void SavePcSave()
        {
            if (document == null) throw new InvalidOperationException("Abra um save de PC antes de salvar.");
            byte[] edited = SerializeData(baselineProjection);
            document.Save(edited);
            editorData = edited;
            foreach (var editor in reserveEditors) editor.AcceptChanges();
            baselineProjection = SerializeData(null);
        }

        private void ReloadEditor()
        {
            loadingSave = true;
            try
            {
                getData();
                foreach (var editor in reserveEditors) editor.LoadData(editorData);
                baselineProjection = SerializeData(null);
                tabMain.Enabled = true;
                saveToolStripMenuItem.Enabled = true;
            }
            finally { loadingSave = false; }
        }

        private CheckBox SlotNone(int story, int slot)
        {
            var controls = Controls.Find("checkBox" + (story == 0 ? "CS" : "HM") + "DigimonPartySlot" + (slot + 1) + "None", true);
            return (CheckBox)controls[0];
        }

        private void WriteOccupancy(PreservingSaveStream stream)
        {
            for (int story = 0; story < 2; story++)
                for (int slot = 0; slot < PcSaveDocument.PartySize; slot++)
                {
                    int offset = PcSaveDocument.PartyBase + story * PcSaveDocument.StoryOffset + slot * PcSaveDocument.SlotSize;
                    stream.Position = offset;
                    // Preserve the existing marker for an occupied slot.
                    stream.WriteByte(SlotNone(story, slot).Checked ? (byte)0 : editorData[offset]);
                }
        }

        private bool ChangePartySlot(int story, int slot)
        {
            if (loadingSave || document == null) return true;
            CheckBox none = SlotNone(story, slot);
            int start = PcSaveDocument.PartyBase + story * PcSaveDocument.StoryOffset;
            int target = start + slot * PcSaveDocument.SlotSize;
            if (none.Checked || editorData[target] != 0) return true;
            try
            {
                // Serialize pending edits while this still-empty slot is excluded.
                loadingSave = true;
                none.Checked = true;
                byte[] pending = SerializeData(baselineProjection);
                int donor = -1;
                for (int i = 0; i < PcSaveDocument.PartySize; i++)
                    if (pending[start + i * PcSaveDocument.SlotSize] != 0) { donor = i; break; }
                if (donor < 0)
                    throw new InvalidOperationException("Esta party não tem um Digimon para copiar. Adicione um pelo jogo primeiro.");
                int indexOffset = FindPartyIndexOffset(pending, start);
                Buffer.BlockCopy(pending, start + donor * PcSaveDocument.SlotSize, pending, target, PcSaveDocument.SlotSize);
                Buffer.BlockCopy(BitConverter.GetBytes((uint)slot), 0, pending, target + indexOffset, 4);
                editorData = pending;
                ReloadEditor();
                return true;
            }
            catch (Exception ex)
            {
                none.Checked = true;
                MessageBox.Show(this, ex.Message, "Não foi possível preencher o slot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            finally { loadingSave = false; }
        }

        internal static int FindPartyIndexOffset(byte[] data, int start)
        {
            // The transcript confirms an entry stores its party position, but
            // does not give the offset. Require evidence in this actual save;
            // never choose an unknown header field by guessing.
            int result = -1;
            foreach (int candidate in new[] { 4, 8 })
            {
                int seen = 0;
                bool matches = true;
                for (int slot = 0; slot < PcSaveDocument.PartySize; slot++)
                {
                    int offset = start + slot * PcSaveDocument.SlotSize;
                    if (data[offset] == 0) continue;
                    seen++;
                    if (BitConverter.ToUInt32(data, offset + candidate) != slot) matches = false;
                }
                if (matches && seen >= 2)
                {
                    if (result != -1) throw new InvalidDataException("O índice da party é ambíguo. Apenas os Digimons existentes podem ser editados neste save.");
                    result = candidate;
                }
            }
            if (result == -1)
                throw new InvalidDataException("Não foi possível identificar o índice da party com segurança. Mantenha dois Digimons na party pelo jogo antes de preencher slots vazios.");
            return result;
        }

        private static byte[] EncodeSaveName(string text, int size)
        {
            byte[] encoded = new UTF8Encoding(false, true).GetBytes(text);
            if (encoded.Length >= size)
                throw new InvalidDataException("O nome excede o limite de " + (size - 1) + " bytes UTF-8.");
            byte[] result = new byte[size];
            Buffer.BlockCopy(encoded, 0, result, 0, encoded.Length);
            return result;
        }
    }
}
