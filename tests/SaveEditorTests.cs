using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using DigimonStorySaveEditor;
using MainMenu = DigimonStorySaveEditor.MainMenu;
using DigimonStorySaveEditor.SaveFormats;

class SaveEditorTests
{
    static int passed, failed;
    static string root;
    [STAThread] static int Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        root = Path.Combine(Path.GetTempPath(), "dscs-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Run("Cipher matches C++ known vector", CipherVector);
            Run("Cipher round trips complete save", () => {
                var data = new byte[PcSaveDocument.PcSize]; new Random(5531).NextBytes(data);
                Equal(data, PcSaveCipher.Decrypt(PcSaveCipher.Encrypt(data)));
            });
            Run("Cipher rejects partial blocks", () => Throws<ArgumentException>(() => PcSaveCipher.Decrypt(new byte[17])));
            Run("All eight layout boundaries preserve bytes", LayoutBoundaries);
            Run("Unsupported files rejected without writes", RejectFiles);
            Run("UI opens and closes without selecting a save", () => { using(var form=new MainMenu()) { form.Show(); Application.DoEvents(); form.Close(); } });
            Run("Opening is read-only; no-op UI save is byte-identical", NoOp);
            Run("Unstarted Hacker's Memory preserves Japanese default name", () => {
                var data=Fixture();Array.Clear(data,Slot(1,0),11*PcSaveDocument.SlotSize);
                string path=NewSave(data);byte[] before=File.ReadAllBytes(path);
                using(var f=new MainMenu()) { f.OpenPcSave(path);f.SavePcSave(); }
                Equal(before,File.ReadAllBytes(path));
            });
            Run("Only edited money field changes in either campaign", Money);
            Run("Fractional training stats survive unrelated edits", Bonus);
            Run("UTF-8 names survive no-op and can be edited", Names);
            Run("Unknown data and empty slots survive editing", UnknownData);
            Run("Unknown and empty species IDs preserve their original bytes", () => {
                var raw=Fixture();Put(raw,Slot(0,0)+0x10,65535);Put(raw,Slot(1,0)+0x10,999);
                string path=NewSave(raw);byte[] original=File.ReadAllBytes(path);
                using(var f=new MainMenu()) { f.OpenPcSave(path);f.SavePcSave(); }
                Equal(original,File.ReadAllBytes(path));
            });
            Run("First backup is preserved across repeated saves", Backup);
            Run("External changes are not overwritten", ExternalChanges);
            Run("All six portraits respond to species selection", Portraits);
            Run("Empty slots clone party data with their own index", CloneSlots);
            Run("Removing a Digimon changes only its occupancy marker", RemoveSlot);
            Run("Switching files resets stale skill and slot flags", Reload);
            Run("Failed open retains the previous document", FailedOpen);
            Run("Ambiguous party headers refuse index guessing", IndexValidation);
            Run("Both campaigns expose all 11 party positions", AllPartySlots);
            Run("Full parties survive no-op without changing reserve data", ReserveNoOp);
            Run("Reserve stat limits match the original battle slots", ReserveLimits);
            Run("Each reserve HP edit reaches only the selected 16-bit field", ReserveHp);
            Run("Reserve equipment and precision stats preserve adjacent bytes", ReserveStats);
            Run("Reserve current and learned skills persist with their flags", ReserveSkills);
            Run("Reserve edits can be reverted after saving", ReserveRevert);
            Run("The last reserve slot can be filled and emptied", ReserveClone);
            Run("Reloading an empty reserve does not save stale controls", ReserveReload);
            Run("Validation checks the last reserve species", ReserveValidation);
            Run("Gender choices persist in the full CS sex field", Gender);
            Run("An unknown gender is preserved until explicitly changed", UnknownGender);
            Run("Out-of-range values open safely and remain byte-identical", OutOfRangeOpen);
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine(passed + " passed; " + failed + " failed. Synthetic fixtures; game acceptance not tested.");
        return failed == 0 ? 0 : 1;
    }
    static void Run(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine("PASS " + name); }
        catch(Exception ex) { failed++; Console.WriteLine("FAIL " + name + "\n" + ex); }
    }
    static void Assert(bool value, string message) { if(!value) throw new Exception(message); }
    static void Equal(byte[] a, byte[] b) { Assert(a.SequenceEqual(b), "Bytes differ"); }
    static void Throws<T>(Action action) where T:Exception
    {
        try { action(); } catch(T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    static T Control<T>(MainMenu f, string name) where T:System.Windows.Forms.Control { return (T)f.Controls.Find(name,true)[0]; }
    static void Put(byte[] b, int at, uint value) { Buffer.BlockCopy(BitConverter.GetBytes(value),0,b,at,4); }
    static int Slot(int story,int slot) { return PcSaveDocument.PartyBase+story*PcSaveDocument.StoryOffset+slot*PcSaveDocument.SlotSize; }
    static byte[] Fixture()
    {
        var data=new byte[PcSaveDocument.EditorSize]; new Random(735).NextBytes(data);
        Put(data,0,19);
        for(int story=0;story<2;story++)
        {
            int delta=story*PcSaveDocument.StoryOffset;
            Array.Clear(data,Slot(story,0),11*PcSaveDocument.SlotSize);
            Array.Clear(data,0x4B980+delta,0x44);
            Put(data,0x4B980+delta,1);
            var name=Encoding.UTF8.GetBytes(story==0 ? "Takumi" : "天沢ケイスケ");
            Buffer.BlockCopy(name,0,data,0x4B984+delta,name.Length);
            Put(data,0x4B9B8+delta,1234);
            for(int slot=0;slot<3;slot+=2)
            {
                int start=Slot(story,slot);
                data[start]=1;
                Put(data,start+4,37); Put(data,start+8,(uint)slot);
                Put(data,start+12,0xABC000U+(uint)(slot+story*3));
                Put(data,start+0x10,50); Put(data,start+0x14,1); Put(data,start+0x18,1); Put(data,start+0x1C,3);
                var nick=Encoding.UTF8.GetBytes("アグモン"); Buffer.BlockCopy(nick,0,data,start+0x20,nick.Length);
                data[start+0x74]=5; data[start+0x78]=50;
                data[start+0x90]=0xD0; data[start+0x91]=1; // 4.64 HP training points
                data[start+0x9C]=123; // 1.23 SP training points
                for(int i=0;i<26;i++) Put(data,start+0xB4+i*8,0xFFFFFFFF);
                // Unknown overflow moves and tail fields must remain untouched.
                for(int i=0x184;i<0x224;i++) data[start+i]=(byte)(i*11);
            }
        }
        return data;
    }
    static string NewSave(byte[] layout=null)
    {
        string dir=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"0000.bin");
        File.WriteAllBytes(path,PcSaveCipher.Encrypt(PcSaveDocument.Compact(layout??Fixture())));
        return path;
    }
    static byte[] Read(string path) { return PcSaveDocument.Expand(PcSaveCipher.Decrypt(File.ReadAllBytes(path))); }
    static void OnlyRanges(byte[] before,byte[] after,params int[] ranges)
    {
        for(int i=0;i<before.Length;i++)
        {
            bool allowed=false;for(int r=0;r<ranges.Length;r+=2) if(i>=ranges[r] && i<ranges[r]+ranges[r+1]) allowed=true;
            if(!allowed && before[i]!=after[i]) throw new Exception("Unexpected change at 0x"+i.ToString("X"));
        }
    }
    static void CipherVector()
    {
        byte[] input=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        var output=PcSaveCipher.Encrypt(input);
        string actual=BitConverter.ToString(SHA256.Create().ComputeHash(output)).Replace("-","").ToLowerInvariant();
        Assert(actual=="ef398a41fde820f49592936e2d2c641a7fd0e6fbefd6f37cf2f945d2e6e40108", "Does not match MVGLTools C++ output");
    }
    static void LayoutBoundaries()
    {
        var input=new byte[PcSaveDocument.PcSize];new Random(13).NextBytes(input);
        byte[] expanded=PcSaveDocument.Expand(input);
        int[] gaps={0x4BA0C,0x4BA9C,0x4BACC,0x4BB0C,0xAD1AC,0xAD23C,0xAD26C,0xAD2AC};
        for(int i=0;i<gaps.Length;i++)
        {
            int gap=gaps[i], pc=gap-i*4;
            Assert(expanded[gap-1]==input[pc-1] && expanded[gap+4]==input[pc],"Wrong gap mapping");
            Assert(expanded.Skip(gap).Take(4).All(b=>b==0),"Missing gap");
        }
        Equal(input,PcSaveDocument.Compact(expanded));
    }
    static void RejectFiles()
    {
        string path=NewSave();byte[] bytes=File.ReadAllBytes(path);
        foreach(string name in new[]{"slot_0000.bin","system_data.bin"})
        {
            string bad=Path.Combine(Path.GetDirectoryName(path),name);File.WriteAllBytes(bad,bytes);
            Throws<InvalidDataException>(()=>PcSaveDocument.Open(bad));Equal(bytes,File.ReadAllBytes(bad));
        }
        File.WriteAllBytes(path,new byte[843600]);Throws<InvalidDataException>(()=>PcSaveDocument.Open(path));
        File.WriteAllBytes(path,new byte[50]);Throws<InvalidDataException>(()=>PcSaveDocument.Open(path));
        File.WriteAllBytes(path,new byte[PcSaveDocument.PcSize]);Throws<InvalidDataException>(()=>PcSaveDocument.Open(path));
        Assert(!File.Exists(path+".bak"),"Opening created a backup");
    }
    static void NoOp()
    {
        string path=NewSave();byte[] initial=File.ReadAllBytes(path);
        using(var form=new MainMenu())
        {
            form.OpenPcSave(path);Equal(initial,File.ReadAllBytes(path));Assert(!File.Exists(path+".bak"),"Opening writes files");
            form.SavePcSave();Equal(initial,File.ReadAllBytes(path));
            form.Show();Application.DoEvents();form.Close();
        }
    }
    static void Money()
    {
        for(int story=0;story<2;story++)
        {
            string path=NewSave();byte[] before=Read(path);int at=0x4B9B8+story*PcSaveDocument.StoryOffset;
            using(var f=new MainMenu()) { f.OpenPcSave(path);Control<NumericUpDown>(f,"numericUpDown"+(story==0?"CS":"HM")+"CharacterStatsMoney").Value=87654;f.SavePcSave(); }
            byte[] after=Read(path);Assert(BitConverter.ToUInt32(after,at)==87654,"Money not saved");OnlyRanges(before,after,at,4);
        }
    }
    static void Bonus()
    {
        string path=NewSave();byte[] before=Read(path);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);Control<NumericUpDown>(f,"numericUpDownCSDigimonPartySlot1BonusHP").Value=5;f.SavePcSave();
        }
        byte[] after=Read(path);Assert(BitConverter.ToUInt16(after,Slot(0,0)+0x90)==500,"Wrong training stat");OnlyRanges(before,after,Slot(0,0)+0x90,2);
    }
    static void Names()
    {
        string path=NewSave();byte[] before=Read(path);int at=0x4B984+PcSaveDocument.StoryOffset;
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);Assert(Control<TextBox>(f,"textBoxHMCharacterStatsCharacterName").Text=="天沢ケイスケ","Japanese name was damaged on read");
            Control<TextBox>(f,"textBoxHMCharacterStatsCharacterName").Text="新しい名前";f.SavePcSave();
        }
        byte[] after=Read(path);Assert(Encoding.UTF8.GetString(after,at,40).Split('\0')[0]=="新しい名前","UTF-8 name was not saved");OnlyRanges(before,after,at,40);
    }
    static void UnknownData()
    {
        string path=NewSave();byte[] before=Read(path);
        using(var f=new MainMenu()) { f.OpenPcSave(path);Control<TextBox>(f,"textBoxCSDigimonPartySlot1Nickname").Text="New name";f.SavePcSave(); }
        OnlyRanges(before,Read(path),Slot(0,0)+0x20,72);
    }
    static void Backup()
    {
        string path=NewSave();byte[] original=File.ReadAllBytes(path);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);f.SavePcSave();Equal(original,File.ReadAllBytes(path+".bak"));
            Control<NumericUpDown>(f,"numericUpDownCSCharacterStatsMoney").Value=56;f.SavePcSave();Equal(original,File.ReadAllBytes(path+".bak"));
        }
        Assert(Directory.GetFiles(Path.GetDirectoryName(path),"*.tmp").Length==0,"Temporary file leaked");
    }
    static void ExternalChanges()
    {
        string path=NewSave();var doc=PcSaveDocument.Open(path);byte[] other=File.ReadAllBytes(path);other[33]^=1;File.WriteAllBytes(path,other);
        Throws<IOException>(()=>doc.Save(doc.Data));Equal(other,File.ReadAllBytes(path));Assert(!File.Exists(path+".bak"),"Backup created on conflict");
    }
    static void Portraits()
    {
        var raw=Fixture();
        for(int story=0;story<2;story++) Buffer.BlockCopy(raw,Slot(story,0),raw,Slot(story,1),PcSaveDocument.SlotSize);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(NewSave(raw));
            for(int story=0;story<2;story++) for(int slot=1;slot<=3;slot++)
            {
                string name=(story==0?"CS":"HM")+"DigimonPartySlot"+slot;
                var picture=Control<PictureBox>(f,"pictureBox"+name+"Portrait");var before=picture.Image;
                Control<ComboBox>(f,"comboBox"+name+"ID").SelectedItem="AeroVeedramon";
                Assert(picture.Image!=null && !Object.ReferenceEquals(before,picture.Image),"Portrait did not change: "+name);
            }
        }
    }
    static void CloneSlots()
    {
        for(int story=0;story<2;story++)
        {
            string path=NewSave();byte[] before=Read(path);int target=Slot(story,1);
            using(var f=new MainMenu())
            {
                f.OpenPcSave(path);string name=(story==0?"CS":"HM")+"DigimonPartySlot2";
                Control<CheckBox>(f,"checkBox"+name+"None").Checked=false;
                Control<ComboBox>(f,"comboBox"+name+"ID").SelectedItem="AeroVeedramon";f.SavePcSave();
            }
            byte[] after=Read(path);Assert(after[target]!=0,"Slot stayed empty");Assert(BitConverter.ToUInt32(after,target+8)==1,"Donor index was copied");
            Assert(BitConverter.ToUInt32(after,target+0x10)==400,"Species not saved");OnlyRanges(before,after,target,PcSaveDocument.SlotSize);
        }
    }
    static void RemoveSlot()
    {
        string path=NewSave();byte[] before=Read(path);
        using(var f=new MainMenu()) { f.OpenPcSave(path);Control<CheckBox>(f,"checkBoxHMDigimonPartySlot3None").Checked=true;f.SavePcSave(); }
        byte[] after=Read(path);Assert(after[Slot(1,2)]==0,"Slot stayed occupied");OnlyRanges(before,after,Slot(1,2),1);
    }
    static void Reload()
    {
        byte[] second=Fixture();int at=Slot(0,0)+0xB4;Put(second,at,1);Put(second,at+4,3);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(NewSave());Assert(Control<CheckBox>(f,"checkBoxCSDigimonPartySlot1CurrentSkill1None").Checked,"Expected empty skill");
            f.OpenPcSave(NewSave(second));Assert(!Control<CheckBox>(f,"checkBoxCSDigimonPartySlot1CurrentSkill1None").Checked,"Stale skill None flag");
        }
    }
    static void FailedOpen()
    {
        string path=NewSave();string bad=NewSave();File.WriteAllBytes(bad,new byte[32]);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);Throws<InvalidDataException>(()=>f.OpenPcSave(bad));
            Control<NumericUpDown>(f,"numericUpDownCSCharacterStatsMoney").Value=4321;f.SavePcSave();
        }
        Assert(BitConverter.ToUInt32(Read(path),0x4B9B8)==4321,"Previous document lost");
    }
    static void IndexValidation()
    {
        byte[] data=Fixture();Assert(MainMenu.FindPartyIndexOffset(data,Slot(0,0))==8,"Wrong index field");
        Put(data,Slot(0,0)+4,0);Put(data,Slot(0,2)+4,2);
        Throws<InvalidDataException>(()=>MainMenu.FindPartyIndexOffset(data,Slot(0,0)));
    }
    static byte[] FullPartyFixture()
    {
        byte[] data=Fixture();
        for(int story=0;story<2;story++) for(int slot=3;slot<11;slot++)
        {
            int start=Slot(story,slot);
            Buffer.BlockCopy(data,Slot(story,0),data,start,PcSaveDocument.SlotSize);
            Put(data,start+8,(uint)slot);Put(data,start+12,0x12340000U+(uint)(story*11+slot));
            Buffer.BlockCopy(BitConverter.GetBytes((short)(7000+slot)),0,data,start+0x88,2);
            data[start+0x8A]=0xA5;data[start+0x8B]=0x5A; // unknown bytes after current HP
            var name=Encoding.UTF8.GetBytes("Reserve "+(slot+1));Array.Clear(data,start+0x20,72);Buffer.BlockCopy(name,0,data,start+0x20,name.Length);
            data[start+0x92]=0x42;data[start+0x93]=0x75; // unknown adjacent to trained HP
        }
        return data;
    }
    static string ReserveName(int story,int slot) { return (story==0?"CS":"HM")+"DigimonPartySlot"+(slot+1); }
    static void AllPartySlots()
    {
        using(var f=new MainMenu())
        {
            f.OpenPcSave(NewSave(FullPartyFixture()));
            foreach(string story in new[]{"CS","HM"})
                Assert(Control<TabControl>(f,"tabControl"+story+"DigimonPartySlots").TabCount==11,"Party is still truncated");
            for(int story=0;story<2;story++) for(int slot=3;slot<11;slot++)
            {
                string name=ReserveName(story,slot);
                Assert(Control<TextBox>(f,"textBox"+name+"Nickname").Text=="Reserve "+(slot+1),"Wrong reserve name");
                Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"CurrentHP").Value==7000+slot,"Truncated reserve HP");
                var picture=Control<PictureBox>(f,"pictureBox"+name+"Portrait");var initial=picture.Image;
                var combo=Control<ComboBox>(f,"comboBox"+name+"ID");combo.SelectedIndex=combo.FindStringExact("AeroVeedramon");
                Assert(picture.Image!=null && !Object.ReferenceEquals(initial,picture.Image),"Stale reserve portrait");
            }
        }
    }
    static void ReserveNoOp()
    {
        string path=NewSave(FullPartyFixture());byte[] original=File.ReadAllBytes(path);
        using(var f=new MainMenu()) { f.OpenPcSave(path);f.SavePcSave(); }
        Equal(original,File.ReadAllBytes(path));
    }
    static void ReserveLimits()
    {
        using(var f=new MainMenu())
        {
            f.OpenPcSave(NewSave(FullPartyFixture()));string name=ReserveName(0,3);
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"CurrentLVL").Maximum==99,"Wrong level limit");
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"Defense").Maximum==1500,"Wrong defense limit");
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"CurrentHP").Maximum==9999,"Wrong HP limit");
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"CurrentSP").Maximum==999,"Wrong SP limit");
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"BonusAttack").Maximum==327,"Wrong training limit");
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"ABI").Maximum==200,"Wrong ABI limit");
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"CAM").Maximum==100,"Wrong CAM limit");
        }
    }
    static void ReserveHp()
    {
        string path=NewSave(FullPartyFixture());
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);
            for(int story=0;story<2;story++) for(int slot=3;slot<11;slot++)
            {
                byte[] before=Read(path);int at=Slot(story,slot)+0x88;
                Control<NumericUpDown>(f,"numericUpDown"+ReserveName(story,slot)+"CurrentHP").Value=9000+slot;
                f.SavePcSave();byte[] after=Read(path);
                Assert(BitConverter.ToInt16(after,at)==9000+slot,"HP written to wrong reserve");OnlyRanges(before,after,at,2);
            }
        }
    }
    static void ReserveStats()
    {
        string path=NewSave(FullPartyFixture());byte[] before=Read(path);int at=Slot(1,10);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);string name=ReserveName(1,10);
            Assert(Control<NumericUpDown>(f,"numericUpDown"+name+"BonusHP").Value==4.64m,"Reserve training HP lost precision");
            Control<NumericUpDown>(f,"numericUpDown"+name+"BonusHP").Value=4.65m;
            var equipment=Control<ComboBox>(f,"comboBox"+name+"Equip3");equipment.SelectedIndex=equipment.FindStringExact("HP Attach E");
            var personality=Control<ComboBox>(f,"comboBox"+name+"Personality");personality.SelectedIndex=personality.FindStringExact("Nimble (SPD)");
            f.SavePcSave();
        }
        byte[] after=Read(path);Assert(BitConverter.ToUInt16(after,at+0x90)==465,"Wrong training encoding");
        Assert(BitConverter.ToUInt16(after,at+0x22E)==301,"Equipment not saved");Assert(BitConverter.ToUInt32(after,at+0x84)==6,"Wrong personality ID");
        OnlyRanges(before,after,at+0x90,2,at+0x22E,2,at+0x84,4);
    }
    static void ReserveSkills()
    {
        string path=NewSave(FullPartyFixture());byte[] before=Read(path);int start=Slot(0,3);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);string name=ReserveName(0,3);
            foreach(string kind in new[]{"CurrentSkill1","LearnedSkill20"})
            {
                Control<CheckBox>(f,"checkBox"+name+kind+"None").Checked=false;
                var combo=Control<ComboBox>(f,"comboBox"+name+kind);combo.SelectedIndex=combo.FindStringExact("Guard");
                Control<CheckBox>(f,"checkBox"+name+kind+"Inherited").Checked=true;
            }
            f.SavePcSave();
        }
        byte[] after=Read(path);int current=start+0xB4, learned=start+0xE4+19*8;
        foreach(int at in new[]{current,learned}) Assert(BitConverter.ToUInt32(after,at)==1 && BitConverter.ToUInt32(after,at+4)==3,"Skill tuple incorrect");
        OnlyRanges(before,after,current,8,learned,8);
    }
    static void ReserveRevert()
    {
        string path=NewSave(FullPartyFixture());byte[] original=File.ReadAllBytes(path);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);var hp=Control<NumericUpDown>(f,"numericUpDown"+ReserveName(1,7)+"CurrentHP");decimal initial=hp.Value;
            hp.Value=8888;f.SavePcSave();hp.Value=initial;f.SavePcSave();
        }
        Equal(original,File.ReadAllBytes(path));
    }
    static void ReserveClone()
    {
        string path=NewSave();byte[] before=Read(path);int start=Slot(1,10);string name=ReserveName(1,10);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);Control<CheckBox>(f,"checkBox"+name+"None").Checked=false;
            var combo=Control<ComboBox>(f,"comboBox"+name+"ID");combo.SelectedIndex=combo.FindStringExact("AeroVeedramon");f.SavePcSave();
            byte[] filled=Read(path);Assert(filled[start]==1 && BitConverter.ToUInt32(filled,start+8)==10,"Wrong reserve clone index");
            Assert(BitConverter.ToUInt32(filled,start+0x10)==400,"Wrong cloned species");OnlyRanges(before,filled,start,PcSaveDocument.SlotSize);
            Control<CheckBox>(f,"checkBox"+name+"None").Checked=true;f.SavePcSave();
            byte[] removed=Read(path);Assert(removed[start]==0,"Reserve was not emptied");OnlyRanges(filled,removed,start,1);
        }
    }
    static void ReserveReload()
    {
        string empty=NewSave();byte[] original=File.ReadAllBytes(empty);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(NewSave(FullPartyFixture()));f.OpenPcSave(empty);
            Assert(Control<CheckBox>(f,"checkBox"+ReserveName(0,10)+"None").Checked,"Stale reserve occupancy");
            Assert(!Control<ComboBox>(f,"comboBox"+ReserveName(0,10)+"ID").Enabled,"Empty reserve still allows species edits");f.SavePcSave();
        }
        Equal(original,File.ReadAllBytes(empty));
    }
    static void ReserveValidation()
    {
        var raw=FullPartyFixture();Put(raw,Slot(1,10)+0x10,12345);string path=NewSave(raw);
        Throws<InvalidDataException>(()=>PcSaveDocument.Open(path));
    }
    static void Gender()
    {
        string path=NewSave();byte[] original=File.ReadAllBytes(path);byte[] before=Read(path);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);var combo=Control<ComboBox>(f,"comboBoxCSCharacterStatsCharacterGender");
            Assert(combo.DropDownStyle==ComboBoxStyle.DropDownList && combo.Items.Count==2,"Missing gender choices");
            Assert(combo.Text=="Male","Wrong initial gender");combo.SelectedItem="Female";f.SavePcSave();
            byte[] after=Read(path);Assert(BitConverter.ToUInt32(after,0x4B980)==2,"Female not saved");OnlyRanges(before,after,0x4B980,4);
            f.OpenPcSave(path);Assert(combo.Text=="Female","Female not reloaded");combo.SelectedItem="Male";f.SavePcSave();
        }
        Equal(original,File.ReadAllBytes(path));
    }
    static void UnknownGender()
    {
        var data=Fixture();Put(data,0x4B980,0xA1B2C3D4);string path=NewSave(data);byte[] original=File.ReadAllBytes(path);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);f.SavePcSave();Equal(original,File.ReadAllBytes(path));
            Control<ComboBox>(f,"comboBoxCSCharacterStatsCharacterGender").SelectedItem="Female";f.SavePcSave();
        }
        Assert(BitConverter.ToUInt32(Read(path),0x4B980)==2,"Only one byte of the gender field was written");
    }
    static void OutOfRangeOpen()
    {
        var data=FullPartyFixture();int battle=Slot(0,0),reserve=Slot(1,10);
        Buffer.BlockCopy(BitConverter.GetBytes((short)102),0,data,battle+0xB0,2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)102),0,data,reserve+0x74,2);
        string path=NewSave(data);byte[] original=File.ReadAllBytes(path);
        using(var f=new MainMenu())
        {
            f.OpenPcSave(path);
            Assert(Control<NumericUpDown>(f,"numericUpDownCSDigimonPartySlot1CAM").Value==100,"Battle value was not clamped for display");
            Assert(Control<NumericUpDown>(f,"numericUpDownHMDigimonPartySlot11CurrentLVL").Value==99,"Reserve value was not clamped for display");
            f.SavePcSave();
        }
        Equal(original,File.ReadAllBytes(path));
    }

}
