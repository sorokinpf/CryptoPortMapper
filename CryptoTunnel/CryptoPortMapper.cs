using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;

namespace CryptoTunnel
{
    class Client
    {
        public static SslStream CreateConnectionStream(string remote_server, int remote_port, string certificate_path)
        {
            TcpClient client = new TcpClient(remote_server, remote_port);


            X509Certificate2 cert;
            if (certificate_path != null)
            { 
                cert = new X509Certificate2(certificate_path);

                SslStream stream = new SslStream(client.GetStream(), false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                stream.AuthenticateAsClient(remote_server, new X509Certificate2Collection(cert), System.Security.Authentication.SslProtocols.Tls, false);
                return stream;
            }
            else
            {
                return new SslStream(client.GetStream());
            }
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
    class ForwarderThread
    {
        static int opened = 0;

        public async static Task<int> recv(Stream stream,byte[] buffer,int count,int Timeout)
        {
            var bytes = 0;
            var receiveTask = Task.Run(async () => { bytes = await stream.ReadAsync(buffer, 0, count); });
            var isReceived = await Task.WhenAny(receiveTask, Task.Delay(Timeout)) == receiveTask;
            return bytes;
        }

        static int read(Stream stream, byte[] buffer, int count, int Timeout)
        {
            stream.ReadTimeout = Timeout;
            int bytes = 0;
            try
            {
                bytes = stream.Read(buffer, 0, count);
            }
            catch (System.IO.IOException ex)
            {
                var x = ex.InnerException as SocketException;
                if (x != null)
                {
                    if (x.ErrorCode == 10060)
                        return 0; /// timeout
                    //System.Console.WriteLine(x.ErrorCode);
                }
                return -1; 
            }
            if (bytes == 0)
                return -1;
            return bytes;

        }
        public static void func(object input_data)//, object out_stream_obj)
        {
            opened += 1;
            
            ArrayList streams = (ArrayList)input_data;
            Stream in_stream = (Stream)streams[0];
            Stream out_stream = (Stream)streams[1];
            int reread_timeout = (int)streams[2];
            Console.WriteLine("Threads: {0}, {1} started", opened,in_stream.GetType().Name);
            int base_timeout = 5000;

            byte[] buffer = new byte[1024 * 1024];
            byte[] buffer2 = new byte[1024 * 1024];
            while (true)
            {
                try
                {
                    if (out_stream.CanWrite == false) break;
                    int bytes = read(in_stream,buffer, 1024 * 1024, base_timeout);
                    if (bytes == 0)
                       continue; // no data; Check connection and read again
                    if (bytes == -1) // socket error
                        break;
                    int bytes2 = 0;
                    if (reread_timeout > 0)
                    {
                        bytes2 = read(in_stream, buffer2, 1024 * 1024, reread_timeout);
                        if (bytes2 > 0)
                        {
                            //System.Console.WriteLine("bytes: {0},{1}", bytes, bytes2);
                            System.Buffer.BlockCopy(buffer2, 0, buffer, bytes, bytes2);
                            bytes += bytes2;
                        }
                    }

                    System.Console.WriteLine("message received {0} size:{1},{2}", in_stream.GetType(), bytes,bytes2);
                    if (out_stream.CanWrite == false) break;
                    Decoder decoder = Encoding.UTF8.GetDecoder();
                    char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                    decoder.GetChars(buffer, 0, bytes, chars, 0);
                    Console.WriteLine(new StringBuilder().Append(chars));
                    //System.Console.WriteLine("WRITING!!!!!!!");
                    out_stream.Write(buffer,0,bytes);
                }
                catch(System.IO.IOException ex)
                {                    
                    Console.WriteLine(ex.Message);
                    break;
                }
            }
            in_stream.Dispose();
            opened -= 1;
            Console.WriteLine("Threads: {0}, {1} finished", opened, in_stream.GetType().Name);
        }
    }
    class Server
    {
        TcpListener Listener;
        string remote_server;
        string certificate_path;
        int remote_port;
        int reread_timeout;

        public Server(string local_ip, int local_port,string remote_server, int remote_port,int reread_timeout, string certificate_path)
        {
            Listener = new TcpListener(IPAddress.Parse(local_ip),local_port);
            this.remote_server = remote_server;
            this.remote_port = remote_port;
            this.certificate_path = certificate_path;
            this.reread_timeout = reread_timeout;
        }

        public void Start()
        {
            Listener.Start();
            while (true)
            {
                TcpClient client = Listener.AcceptTcpClient();
                NetworkStream client_stream = client.GetStream();
                SslStream server_stream = Client.CreateConnectionStream(remote_server, remote_port, certificate_path);

                Thread thread1 = new Thread(ForwarderThread.func);
                ArrayList streams1 = new ArrayList();
                streams1.Add(client_stream);
                streams1.Add(server_stream);
                streams1.Add(0);
                thread1.Start(streams1);

                Thread thread2 = new Thread(ForwarderThread.func);
                ArrayList streams2 = new ArrayList();
                streams2.Add(server_stream);
                streams2.Add(client_stream);
                streams2.Add(this.reread_timeout);
                thread2.Start(streams2);
            }
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            string usage = "usage: cpm.exe <listen_ip> <listen_port> <remote_hostname> <remote_port> <reread_timeout> [<certificate_path>]";
            if ((args.Length < 5) | (args.Length >6))
            {
                Console.WriteLine(usage);
                return;
            }

            string listen_ip = args[0];
            int listen_port = int.Parse(args[1]);
            string server = args[2];
            int remote_port = int.Parse(args[3]);
            int reread_timeout = int.Parse(args[4]);
            string certificate_path = null;
            if (args.Length == 6)
                certificate_path = args[5];
            
            Server s = new Server(listen_ip,listen_port, server, remote_port, reread_timeout, certificate_path);
            s.Start();
        }
    }
}
