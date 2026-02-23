using System.Text;

namespace MuVoidConfigEditor;

/// <summary>
/// Lee/escribe Data\Local\ServerList.bmd (formato Mu Online)
/// </summary>
static class ServerListLoader
{
    private const int MaxServerNameLength = 32;
    private const int MaxServerCount = 15;
    private const int StructSize = 2 + MaxServerNameLength + 1 + 1 + MaxServerCount + 2; // 53

    public static List<ServerListEntry> Load(string path)
    {
        var list = new List<ServerListEntry>();
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        while (fs.Position < fs.Length)
        {
            var buf = br.ReadBytes(StructSize);
            if (buf.Length < StructSize) break;

            BuxConvert.Apply(buf);

            var entry = new ServerListEntry
            {
                Index = BitConverter.ToUInt16(buf, 0),
                Pos = buf[34],
                Sequence = buf[35]
            };

            var nameBytes = new byte[MaxServerNameLength];
            Array.Copy(buf, 2, nameBytes, 0, MaxServerNameLength);
            var nameLen = Array.IndexOf(nameBytes, (byte)0);
            if (nameLen < 0) nameLen = MaxServerNameLength;
            entry.Name = Encoding.UTF8.GetString(nameBytes, 0, nameLen);

            for (int i = 0; i < MaxServerCount; i++)
                entry.NonPvp[i] = buf[36 + i];

            var descLen = BitConverter.ToInt16(buf, 51);
            if (descLen > 0 && descLen < 1024)
            {
                entry.DescriptionRaw = br.ReadBytes(descLen);
                BuxConvert.Apply(entry.DescriptionRaw);
            }

            list.Add(entry);
        }

        return list;
    }

    public static void Save(string path, List<ServerListEntry> entries)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        foreach (var e in entries)
        {
            var buf = new byte[StructSize];
            BitConverter.GetBytes(e.Index).CopyTo(buf, 0);

            var nameBytes = Encoding.UTF8.GetBytes(e.Name ?? "");
            var copyLen = Math.Min(nameBytes.Length, MaxServerNameLength);
            Array.Copy(nameBytes, 0, buf, 2, copyLen);

            buf[34] = e.Pos;
            buf[35] = e.Sequence;
            for (int i = 0; i < MaxServerCount; i++)
                buf[36 + i] = e.NonPvp[i];

            var descLen = (short)e.DescriptionRaw.Length;
            BitConverter.GetBytes(descLen).CopyTo(buf, 51);

            BuxConvert.Apply(buf);
            bw.Write(buf);

            if (descLen > 0)
            {
                var descCopy = (byte[])e.DescriptionRaw.Clone();
                BuxConvert.Apply(descCopy);
                bw.Write(descCopy);
            }
        }
    }
}
