using System.Net.Sockets;

namespace LightService 
{
    public interface IFileService 
    {
        Task<byte[]> LoadFileFromServer();
    }

    public class FileService : IFileService 
    {
        private readonly string _host;
        private readonly int _port;

        public FileService(string host, int port)
        {
            _host = host;
            _port = port;
        }
        public async Task<byte[]> LoadFileFromServer() 
        {
            try
            {
                using TcpClient client = new TcpClient(_host, _port);
                await using NetworkStream stream = client.GetStream();

                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                return ms.ToArray();
            }
            catch (Exception e)
            {
                return Array.Empty<byte>();
            }
        }
    }
}