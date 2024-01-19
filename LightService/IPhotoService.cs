using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightService 
{
    public interface IFileService 
    {
        Task<byte[]> LoadFileFromServer();
        Task WriteServerCommand(string cmd);
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
                TcpClient client = new TcpClient(_host, _port);
                NetworkStream stream = client.GetStream();
                var arr = new byte[1024 * 1024 * 5];
                var ms = new MemoryStream(arr);
                await stream.CopyToAsync(ms);

                return ms.ToArray();
            }
            catch (Exception e)
            {
                return Array.Empty<byte>();
            }
        }

        public async Task WriteServerCommand(string cmd)
        {
            try
            {
                using TcpClient client = new TcpClient(_host, _port);
                await using NetworkStream stream = client.GetStream();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(cmd));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}