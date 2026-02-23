namespace MuVoidConfigEditor;

/// <summary>
/// Encriptación XOR usada por Mu Online en archivos .bmd
/// Mismo algoritmo que _crypt.h: bBuxCode = { 0xFC, 0xCF, 0xAB }
/// </summary>
static class BuxConvert
{
    private static readonly byte[] Key = { 0xFC, 0xCF, 0xAB };

    public static void Apply(Span<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
            data[i] ^= Key[i % 3];
    }

    public static void Apply(byte[] data) => Apply(data.AsSpan());
}
