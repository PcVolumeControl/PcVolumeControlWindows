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
    class Server
    {
        private ClientListener m_clientListener;
        private TcpListener tcpListener;
        private Thread listenThread;
        private List<TcpClient> m_clients = new List<TcpClient>();

        public Server(ClientListener clientListener)
        {
            this.tcpListener = new TcpListener(IPAddress.Any, 3000);
            this.listenThread = new Thread(new ThreadStart(ListenForClients));
            this.listenThread.Start();
            m_clientListener = clientListener;
        }

        private void ListenForClients()
        {
            this.tcpListener.Start();

            while (true)
            {
                //blocks until a client has connected to the server
                TcpClient client = this.tcpListener.AcceptTcpClient();
                Console.WriteLine("connection accepted");
                //create a thread to handle communication 
                //with connected client
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                clientThread.Start(client);
            }
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

            NetworkStream clientStream = tcpClient.GetStream();
            var bufferedStream = new BufferedStream(clientStream);
            var streamReader = new StreamReader(bufferedStream);

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
                        m_clientListener.onClientMessage(message);
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

            lock (this)
            {
                m_clients.Remove(tcpClient);
            }
            tcpClient.Close();

            Console.WriteLine("Client disconnected");
        }

        public void sendData( string data )
        {
            var finalData = data;
            if(data != null && data.Length > 0 && data[data.Length-1] != '\n')
            {
                finalData += '\n';
            }

            lock (this)
            {
                foreach (var client in m_clients)
                {
                    Console.WriteLine("Sending data to a client...");

                    NetworkStream clientStream = client.GetStream();
                    ASCIIEncoding encoder = new ASCIIEncoding();
                    byte[] buffer = encoder.GetBytes(finalData);

                    clientStream.Write(buffer, 0, buffer.Length);
                    clientStream.Flush();
                }
            }
        }
    }

    interface ClientListener
    {
        void onClientMessage( string message );
        void onClientConnect();
    }
}
