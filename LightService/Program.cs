using System.Runtime.InteropServices;
using LightService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/*
import socket

HOST = "127.0.0.1"  # Standard loopback interface address (localhost)
PORT = 4343  # Port to listen on (non-privileged ports are > 1023)

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind((HOST, PORT))
    
    s.listen()
    
    while True:
         try:
            print(f'listening {4343}')
            conn, addr = s.accept()
            print(f'{addr} connection established.')
            f = open("./photo.png", mode="rb")
            print("file opened")
            filedata = f.read()
            print("file read")
            conn.sendall(filedata)
            print('File sent')
            conn.close()
            f.close();
         except:
            print(f"exception {e}")
            pass*/



var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(s =>
{
    s.AddSingleton<IFileService, FileService>(
        _ => new FileService("localhost", 31983));
});

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) builder = builder.UseSystemd();

IHost host = builder
    .ConfigureServices(services => { services.AddHostedService<Worker>(); })
    .Build();

await host.RunAsync();
