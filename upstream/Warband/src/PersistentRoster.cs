using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Warband
{
    public class PersistentRoster
    {
        public string Group = "";
        public List<string> Keys = new List<string>();

        public bool Contains(string key) { return key != null && Keys.Contains(key); }
        public bool Valid(string self)
        {
            Guid group;
            if (!Guid.TryParse(Group, out group) || group == Guid.Empty || Keys == null || Keys.Count < 1 || Keys.Count > 10) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in Keys)
                if (string.IsNullOrEmpty(key) || key.Length > 512 || !seen.Add(key)) return false;
            return Contains(self);
        }
        public bool CanResume(string group, string self, string peer)
        { return Valid(self) && Group == group && Contains(peer) && peer != self; }
        public bool CanAcceptOffer(string group, string self, string peer, bool established, bool alreadyInParty)
        { return !alreadyInParty && CanResume(group, self, peer) && (established || string.CompareOrdinal(peer, self) < 0); }

        public static string Identity(string platform, string character)
        {
            if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(character)) return null;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(platform)) + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(character));
        }
        public static string FileName(long world, string self)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(world + ":" + self))).Replace("-", "") + ".xml";
        }
        public bool IsFirstOnline(string self, IEnumerable<string> online)
        {
            foreach (string peer in online)
                if (Contains(peer) && string.CompareOrdinal(peer, self) < 0) return false;
            return Contains(self);
        }
        public static PersistentRoster Load(string path, string self)
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 32768) throw new InvalidDataException("Party file too large");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings))
            {
                var roster = (PersistentRoster)new XmlSerializer(typeof(PersistentRoster)).Deserialize(reader);
                if (!roster.Valid(self)) throw new InvalidDataException("Invalid saved party");
                return roster;
            }
        }
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var writer = new StreamWriter(temp, false, Encoding.UTF8))
                new XmlSerializer(typeof(PersistentRoster)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
