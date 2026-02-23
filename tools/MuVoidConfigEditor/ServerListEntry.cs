namespace MuVoidConfigEditor;

/// <summary>
/// Una entrada del ServerList.bmd (formato idéntico a SERVER_GROUP_INFO en C++)
/// </summary>
public class ServerListEntry
{
    public ushort Index { get; set; }
    public string Name { get; set; } = "";       // UTF-8 en archivo, mostramos como string
    public byte Pos { get; set; }                // 0=Left, 1=Right, 2=Center
    public byte Sequence { get; set; }
    public byte[] NonPvp { get; set; } = new byte[15];
    public byte[] DescriptionRaw { get; set; } = Array.Empty<byte>();  // bytes crudos preservados
}
