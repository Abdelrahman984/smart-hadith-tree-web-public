using System.IO;
using System.Text;

namespace SmartHadithTree.Infrastructure.Data.Gawami;

public class GawamiTbxReader : IDisposable
{
    private readonly BinaryReader _reader;

    public GawamiTbxReader(string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _reader = new BinaryReader(stream, Encoding.UTF8);
    }

    public string[]? ReadNextRow()
    {
        if (_reader.BaseStream.Position >= _reader.BaseStream.Length) return null;
        
        try
        {
            var line = _reader.ReadString();
            return line.Split('\t');
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }
    
    public void Dispose() => _reader.Dispose();
}
