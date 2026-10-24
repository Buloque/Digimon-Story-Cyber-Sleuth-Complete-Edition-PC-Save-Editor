using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DigimonStorySaveEditor.SaveFormats;

namespace DigimonStorySaveEditor
{
    // The reserve entries have the same 0x240-byte layout as the battle entries.
    // Only changed fields are written; header IDs and unknown regions stay intact.
    internal sealed class PartySlotEditor : UserControl
    {
        private sealed class Choice
        {
            internal uint Id;
            internal string Label;
            public override string ToString() { return Label; }
        }
        private sealed class Field
        {
            internal int Offset;
            internal Action<byte[], int> Load;
            internal Func<object> Value;
            internal Func<byte[]> Encode;
            internal object Initial;
        }
        private readonly List<Field> fields = new List<Field>();
        private readonly TabControl body = new TabControl { Dock = DockStyle.Fill };
        private readonly CheckBox none;
        private readonly PictureBox portrait = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Width = 110, Height = 100 };
        private readonly string prefix;
        private readonly ComboBox species;
        private bool loading;
        internal int Story { get; private set; }
        internal int Slot { get; private set; }
        private int Start { get { return PcSaveDocument.PartyBase + Story * PcSaveDocument.StoryOffset + Slot * PcSaveDocument.SlotSize; } }

        internal PartySlotEditor(int story, int slot, Action<int, int> changeSlot)
        {
            Story = story;
            Slot = slot;
            prefix = (story == 0 ? "CS" : "HM") + "DigimonPartySlot" + (slot + 1);
            Name = "editor" + prefix;
            Dock = DockStyle.Fill;
            none = new CheckBox { Name = "checkBox" + prefix + "None", Text = "None (empty slot)", AutoSize = true };
            none.CheckedChanged += (sender, args) => {
                if (!loading) changeSlot(Story, Slot);
                species.Enabled = !none.Checked;
                body.Enabled = !none.Checked;
                portrait.Visible = !none.Checked;
            };
            var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 115, AutoScroll = true };
            species = ChoiceField("ID", 0x10, 4, Properties.Resources.ID_Alphabetized);
            header.Controls.Add(portrait);
            header.Controls.Add(new Label { Text = "Reserve slot " + (slot + 1) + " — Digimon", AutoSize = true, Margin = new Padding(5, 12, 5, 3) });
            header.Controls.Add(species);
            header.Controls.Add(none);
            portrait.Name = "pictureBox" + prefix + "Portrait";
            species.SelectedIndexChanged += (sender, args) => UpdatePortrait();
            var main = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
            var mainTab = new TabPage("Stats / equipment");
            mainTab.Controls.Add(main);
            body.TabPages.Add(mainTab);
            AddField(main, "Nickname", TextField("Nickname", 0x20, 72));
            AddField(main, "Digivolution", ChoiceField("Digivolution", 0x1C, 4,
                "0:Egg?\n1:Training I\n2:Training II\n3:Rookie\n4:Champion\n5:Ultimate\n6:Mega\n7:Ultra\n8:Armor\n9:Boss"));
            AddField(main, "Type", ChoiceField("Type", 0x18, 4, "0:Free\n1:Virus\n2:Vaccine\n3:Data"));
            AddField(main, "Attribute", ChoiceField("Attribute", 0x14, 4,
                "0:Neutral\n1:Fire\n2:Water\n3:Plant\n4:Electric\n5:Earth\n6:Wind\n7:Light\n8:Dark"));
            AddField(main, "Personality", ChoiceField("Personality", 0x84, 4,
                "1:Durable (HP)\n2:Lively (SP)\n3:Fighter (ATK)\n4:Defender (DEF)\n5:Brainy (INT)\n6:Nimble (SPD)\n7:Builder (Development)\n8:Searcher (Investigation)"));
            AddField(main, "Support skill", ChoiceField("SupportSkill", 0x224, 4, Properties.Resources.Support_Skills));
            AddField(main, "Equip slots", Number("EquipSlots", 0x228, 2, true));
            AddField(main, "Memory use", Number("Memory", 0x68, 2, true));
            AddField(main, "EXP", Number("EXP", 0x7C, 4));
            AddField(main, "Current level", Number("CurrentLVL", 0x74, 2, true));
            AddField(main, "Level cap bonus", Number("MaxLVL", 0x76, 2, true));
            AddField(main, "Base level cap", Number("ExtraMaxLVL", 0x78, 2, true));
            AddField(main, "Current HP", Number("CurrentHP", 0x88, 4));
            AddField(main, "Base HP × 10", Number("MaxHP", 0x8C, 4, true, 10m));
            AddField(main, "Bonus HP", Number("BonusHP", 0x90, 2, false, 0.01m));
            AddField(main, "Current SP", Number("CurrentSP", 0x94, 4));
            AddField(main, "Base SP", Number("MaxSP", 0x98, 4));
            AddField(main, "Bonus SP", Number("BonusSP", 0x9C, 2, false, 0.01m));
            string[] stats = { "Attack", "Defense", "Intelligence", "Speed" };
            string[] labels = { "ATK", "DEF", "INT", "SPD" };
            for (int i = 0; i < stats.Length; i++)
            {
                AddField(main, "Base " + labels[i], Number(stats[i], 0x9E + i * 4, 2));
                AddField(main, "Bonus " + labels[i], Number("Bonus" + stats[i], 0xA0 + i * 4, 2, false, 0.01m));
            }
            AddField(main, "ABI", Number("ABI", 0xAE, 2));
            AddField(main, "CAM", Number("CAM", 0xB0, 2));
            for (int i = 0; i < 3; i++) AddField(main, "Equipment " + (i + 1), ChoiceField("Equip" + (i + 1), 0x22A + i * 2, 2, Properties.Resources.Equipment));
            AddField(main, "Accessory", ChoiceField("Accessory", 0x230, 2, Properties.Resources.Accessories));
            AddSkills("Current", 6, 0xB4);
            AddSkills("Learned", 20, 0xE4);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static uint Read(byte[] data, int offset, int size)
        {
            return size == 2 ? BitConverter.ToUInt16(data, offset) : BitConverter.ToUInt32(data, offset);
        }
        private static byte[] Encode(uint value, int size)
        {
            return size == 2 ? BitConverter.GetBytes(checked((ushort)value)) : BitConverter.GetBytes(value);
        }
        private static ComboBox Choices(string data)
        {
            var control = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            foreach (string row in data.Split('\n'))
            {
                int colon = row.IndexOf(':');
                uint id;
                if (colon > 0 && uint.TryParse(row.Substring(0, colon), out id))
                    control.Items.Add(new Choice { Id = id, Label = row.Substring(colon + 1).TrimEnd('\r') });
            }
            return control;
        }
        private static uint Selected(ComboBox control) { return ((Choice)control.SelectedItem).Id; }
        private static void Select(ComboBox control, uint id)
        {
            foreach (Choice item in control.Items)
                if (item.Id == id) { control.SelectedItem = item; return; }
            var unknown = new Choice { Id = id, Label = "[ID " + id + "]" };
            control.Items.Add(unknown);
            control.SelectedItem = unknown;
        }
        private ComboBox ChoiceField(string name, int offset, int size, string choices)
        {
            var control = Choices(choices);
            control.Name = "comboBox" + prefix + name;
            fields.Add(new Field {
                Offset = offset, Load = (data, start) => Select(control, Read(data, start + offset, size)),
                Value = () => Selected(control), Encode = () => Encode(Selected(control), size)
            });
            return control;
        }
        private NumericUpDown Number(string name, int offset, int size, bool signed = false, decimal scale = 1m)
        {
            decimal minimum = signed ? (size == 2 ? short.MinValue : int.MinValue) : 0m;
            decimal maximum = signed ? (size == 2 ? short.MaxValue : int.MaxValue) : (size == 2 ? ushort.MaxValue : uint.MaxValue);
            var control = new NumericUpDown {
                Name = "numericUpDown" + prefix + name, Width = 130,
                Minimum = minimum * scale, Maximum = maximum * scale,
                DecimalPlaces = scale < 1m ? 2 : 0, Increment = scale
            };
            fields.Add(new Field {
                Offset = offset,
                Load = (data, start) => {
                    decimal value = signed ? (size == 2 ? BitConverter.ToInt16(data, start + offset) : BitConverter.ToInt32(data, start + offset)) : Read(data, start + offset, size);
                    control.Value = value * scale;
                },
                Value = () => control.Value,
                Encode = () => {
                    decimal value = control.Value / scale;
                    if (value != decimal.Truncate(value)) throw new InvalidDataException(name + " deve ser múltiplo de " + scale + ".");
                    if (signed) return size == 2 ? BitConverter.GetBytes((short)value) : BitConverter.GetBytes((int)value);
                    return Encode((uint)value, size);
                }
            });
            return control;
        }
        private TextBox TextField(string name, int offset, int size)
        {
            var control = new TextBox { Name = "textBox" + prefix + name, Width = 240 };
            fields.Add(new Field {
                Offset = offset, Load = (data, start) => control.Text = Encoding.UTF8.GetString(data, start + offset, size).Split('\0')[0],
                Value = () => control.Text,
                Encode = () => {
                    var value = new UTF8Encoding(false, true).GetBytes(control.Text);
                    if (value.Length >= size) throw new InvalidDataException("O apelido excede " + (size - 1) + " bytes UTF-8.");
                    var result = new byte[size]; Buffer.BlockCopy(value, 0, result, 0, value.Length); return result;
                }
            });
            return control;
        }
        private static void AddField(FlowLayoutPanel parent, string label, Control control)
        {
            var panel = new FlowLayoutPanel { Width = 420, Height = 36, WrapContents = false };
            panel.Controls.Add(new Label { Text = label, Width = 155, Margin = new Padding(3, 6, 3, 3) });
            panel.Controls.Add(control);
            parent.Controls.Add(panel);
        }
        private void AddSkills(string kind, int count, int offset)
        {
            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var page = new TabPage(kind + " skills"); page.Controls.Add(list); body.TabPages.Add(page);
            for (int i = 0; i < count; i++)
            {
                int at = offset + i * 8;
                string name = prefix + kind + "Skill" + (i + 1);
                var combo = Choices(Properties.Resources.Skills); combo.Name = "comboBox" + name;
                var inherited = new CheckBox { Name = "checkBox" + name + "Inherited", Text = "Inherited?", AutoSize = true };
                var empty = new CheckBox { Name = "checkBox" + name + "None", Text = "Empty?", AutoSize = true };
                empty.CheckedChanged += (sender, args) => { combo.Enabled = inherited.Enabled = !empty.Checked; };
                var row = new FlowLayoutPanel { Width = 650, Height = 34, WrapContents = false };
                row.Controls.Add(new Label { Text = "Skill " + (i + 1), Width = 65 });
                row.Controls.Add(combo); row.Controls.Add(inherited); row.Controls.Add(empty); list.Controls.Add(row);
                fields.Add(new Field {
                    Offset = at,
                    Load = (data, start) => {
                        uint flag = Read(data, start + at, 4);
                        empty.Checked = flag > 1; inherited.Checked = flag == 1;
                        Select(combo, Read(data, start + at + 4, 4));
                    },
                    Value = () => empty.Checked ? "empty" : inherited.Checked + ":" + Selected(combo),
                    Encode = () => {
                        byte[] result = new byte[8];
                        Buffer.BlockCopy(BitConverter.GetBytes(empty.Checked ? uint.MaxValue : inherited.Checked ? 1U : 0U), 0, result, 0, 4);
                        Buffer.BlockCopy(BitConverter.GetBytes(empty.Checked ? 0U : Selected(combo)), 0, result, 4, 4);
                        return result;
                    }
                });
            }
        }
        private void UpdatePortrait()
        {
            var choice = species.SelectedItem as Choice;
            portrait.Image = choice == null ? Properties.Resources.field000 :
                (Image)Properties.Resources.ResourceManager.GetObject("field" + choice.Id.ToString("D3")) ?? Properties.Resources.field000;
        }
        internal void LoadData(byte[] data)
        {
            loading = true;
            try
            {
                none.Checked = data[Start] == 0;
                species.Enabled = !none.Checked;
                body.Enabled = !none.Checked;
                portrait.Visible = !none.Checked;
                if (none.Checked) return;
                foreach (Field field in fields) field.Load(data, Start);
                AcceptChanges();
            }
            finally { loading = false; }
        }
        internal void AcceptChanges()
        {
            if (!none.Checked) foreach (Field field in fields) field.Initial = field.Value();
        }
        internal void ApplyChanges(PreservingSaveStream stream)
        {
            if (none.Checked) return;
            foreach (Field field in fields)
                if (!Equals(field.Initial, field.Value()))
                {
                    stream.Position = Start + field.Offset;
                    byte[] value = field.Encode();
                    stream.Write(value, 0, value.Length);
                }
        }
    }
}
