using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VolumeControl
{
    public class Server
    {
        private ClientListener m_clientListener;
        private TcpListener m_tcpListener;
        private Thread listenThread;
        private List<TcpClient> m_clients = new List<TcpClient>();
        private bool m_running = false;
        private ASCIIEncoding m_encoder = new ASCIIEncoding();
        private MdnsService m_mdnsService;
        private IPAddress m_serverAddress;
        private int m_serverPort;

        public Server(ClientListener clientListener, string address, int port)
        {
            var parsedAddress = IPAddress.Parse(address);
            m_serverAddress = parsedAddress;
            m_serverPort = port;
            m_tcpListener = new TcpListener(parsedAddress, port);
            m_clientListener = clientListener;
            m_mdnsService = new MdnsService();
            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.Start();
            Console.WriteLine("Server listening on address: {0}:{1}", parsedAddress, port);
        }

        public bool isRunning()
        {
            return m_running;
        }

        public void stop()
        {
            m_running = false;

            // Stop mDNS service
            m_mdnsService?.Stop();

            m_tcpListener.Stop();

            lock ( this )
            {
                foreach (var client in m_clients)
                {
                    Console.WriteLine("Closing client connection...");

                    try
                    {
                        client.Close();
                    }
                    catch (IOException e)
                    {

                    }
                    catch (ObjectDisposedException e)
                    {

                    }
                }
            }
        }

        private void ListenForClients()
        {
            this.m_tcpListener.Start();

            m_running = true;
            
            // Start mDNS service announcement
            StartMdnsService();
            
            m_clientListener.onServerStart();

            while (m_running)
            {
                //blocks until a client has connected to the server
                try
                {
                    TcpClient client = m_tcpListener.AcceptTcpClient();
                    Console.WriteLine("connection accepted");
                    //hand off communication with the connected client to a
                    //pooled thread so we don't leak a raw Thread per connection
                    ThreadPool.QueueUserWorkItem(HandleClientComm, client);
                }
                catch(SocketException e)
                {

                }
                catch(Exception e)
                {
                    //a non-socket error must not kill the accept loop, or the
                    //server stops accepting connections for good
                    Console.WriteLine("Error accepting client: " + e.Message);
                }
            }

            m_running = false;
            m_clientListener.onServerEnd();
        }

        private void StartMdnsService()
        {
            Task.Run(async () =>
            {
                try
                {
                    if (m_mdnsService == null)
                    {
                        Console.WriteLine("mDNS service is null, cannot start");
                        return;
                    }

                    // Determine the actual server address for mDNS announcement
                    IPAddress announceAddress = m_serverAddress;
                    
                    // If listening on 0.0.0.0, use the first non-loopback IPv4 address
                    if (m_serverAddress.Equals(IPAddress.Any))
                    {
                        string[] localIPs = App.GetLocalIPAddresses();
                        // Skip 0.0.0.0 and find first real IP
                        for (int i = 1; i < localIPs.Length; i++)
                        {
                            if (IPAddress.TryParse(localIPs[i], out IPAddress addr) && 
                                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !IPAddress.IsLoopback(addr))
                            {
                                announceAddress = addr;
                                break;
                            }
                        }
                    }

                    bool success = await m_mdnsService.StartAsync(announceAddress, m_serverPort);
                    if (success)
                    {
                        Console.WriteLine($"mDNS service started successfully on {announceAddress}:{m_serverPort}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to start mDNS service after retries");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error starting mDNS service: {ex.Message}");
                }
            });
        }

        private void HandleClientComm(object client)
        {
            Console.WriteLine("Client connected");

            TcpClient tcpClient = (TcpClient)client;
            lock (this)
            {
                m_clients.Add(tcpClient);
            }

            m_clientListener.onClientConnect();

            try
            {
                NetworkStream clientStream = tcpClient.GetStream();
                using (var bufferedStream = new BufferedStream(clientStream))
                using (var streamReader = new StreamReader(bufferedStream))
                {
                    while (tcpClient.Connected)
                    {
                        string message;
                        try
                        {
                            //blocks until a client sends a message
                            //bytesRead = clientStream.Read(message, 0, 4096);
                            message = streamReader.ReadLine();
                        }
                        catch
                        {
                            //a socket error has occured
                            break;
                        }

                        // End of message
                        if (message != null)
                        {
                            Console.WriteLine("Message received");
                            if (m_clientListener != null)
                            {
                                m_clientListener.onClientMessage(message, tcpClient);
                            }
                            else
                            {
                                Console.WriteLine("Message missed, no listener");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No message from client, close socket.");
                            break;
                        }
                    }
                }
            }
            catch(InvalidOperationException e)
            {

            }
            finally
            {
                lock (this)
                {
                    m_clients.Remove(tcpClient);
                }
                tcpClient.Close();
                tcpClient.Dispose();
                Console.WriteLine("Client disconnected");
            }
        }

        public void sendData(string data)
        {
            var finalData = data;
            if (data != null && data.Length > 0 && data[data.Length - 1] != '\n')
            {
                finalData += '\n';
            }

            List<TcpClient> clients;
            lock (this)
            {
                clients = m_clients.ToList();
            }

            byte[] buffer = m_encoder.GetBytes(finalData);

            foreach (var client in clients)
            {
                Console.WriteLine("Sending data to a client...");

                try
                {
                    NetworkStream clientStream = client.GetStream();

                    clientStream.Write(buffer, 0, buffer.Length);
                    clientStream.Flush();
                }
                catch(IOException e)
                {

                }
                catch (ObjectDisposedException e)
                {

                }
            }
        }

        public void Dispose()
        {
            stop();
            m_mdnsService?.Dispose();
        }
    }

    public interface ClientListener
    {
        void onClientMessage( string message, TcpClient tcpClient);
        void onClientConnect();

        void onServerStart();
        void onServerEnd();
    }
}
