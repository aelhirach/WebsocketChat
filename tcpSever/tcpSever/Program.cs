using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
namespace tcpServer
{
    class Program
    {    /// On peut les créer en tant un objet (une classe) comme StateObject
         /// qui envoie la varialble buffer ainsi que la variable temporaire
        static byte[] buffer = new byte[1024];
        static string temp = "";
        static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
        static private string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        static void Main(string[] args)
        {   serverSocket.Bind(new IPEndPoint(IPAddress.Any, 1083));
            serverSocket.Listen(128);
            AcceptClients(serverSocket);
            Console.ReadKey();
        }
        private static void Handshaking(Socket client)
        {
            byte[] buffer = new byte[1024];
            string headerResponse = "";
            var i = client.Receive(buffer);
            headerResponse = (System.Text.Encoding.UTF8.GetString(buffer)).Substring(0, i);
            // write received data to the console
            //Console.WriteLine(headerResponse);
            var key = headerResponse;
            try {
                key = headerResponse.Replace("ey:", "`")
                                      .Split('`')[1]                     // dGhlIHNhbXBsZSBub25jZQ== \r\n .......
                                      .Replace("\r", "").Split('\n')[0]  // dGhlIHNhbXBsZSBub25jZQ==
                                      .Trim();
            }
            catch { }

            // key should now equal dGhlIHNhbXBsZSBub25jZQ==
            var test1 = AcceptKey(ref key);

            var newLine = "\r\n";

            var response = "HTTP/1.1 101 Switching Protocols" + newLine
                 + "Upgrade: websocket" + newLine
                 + "Connection: Upgrade" + newLine
                 + "Sec-WebSocket-Accept: " + test1 + newLine + newLine
                 //+ "Sec-WebSocket-Protocol: chat, superchat" + newLine
                 //+ "Sec-WebSocket-Version: 13" + newLine
                 ;
            try {
                // which one should I use? none of them fires the onopen method
                client.Send(System.Text.Encoding.UTF8.GetBytes(response)); }
            catch {
                client.Close();
            }
            Thread.Sleep(1000);// wait for client to send a message
           
        }
        static void TransferDone(StateObject state)
        {
            Console.WriteLine("Response from " + state.workingSocket.RemoteEndPoint.ToString()+ " : "+DecodeMessage(state.Buffer.ToArray()));
            Console.WriteLine();
            try
            {
                Send(state.workingSocket);

            }
            catch { }
            
        }
        // BeginAccept permet de récupérer les informations du client qui vient se connecter 
        static void AcceptCallback(IAsyncResult ar)
        {
            

            //récupére les informations du client via le paramètre ar
            Socket listener = (Socket)ar.AsyncState;
            // fin d'acceptation de client
            Socket client = listener.EndAccept(ar);
            Console.WriteLine("Client {0} connected", client.RemoteEndPoint.ToString());

            Handshaking(client);
            
            // accepter un nouveau client 
            AcceptClients(listener);
            //  à partir de moment ou j'accepte le client j'appel la méthode receivedata pour recevoir les données 
            ReceiveData(client);
        }
        static void AcceptClients(Socket listener)
        {
            Console.WriteLine("Waiting for client");
            listener.BeginAccept(AcceptCallback, listener);
        }
        // une méthode qui attent les données 
        static void ReceiveData(Socket client)
        {
            StateObject state = new StateObject();
            state.workingSocket = client;
            // client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,ReceiveCallback, client);
            //client.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveCallback, state);

            client.BeginReceive(state.TempBuffer, 0, state.TempBuffer.Length, SocketFlags.None, ReceiveCallback, state);
        }
        static void ReceiveCallback(IAsyncResult ar)
        {   StateObject clientState = (StateObject)ar.AsyncState;
            try
            {
                int nbrBytes = clientState.workingSocket.EndReceive(ar);
                clientState.AddTempBufferToBuffer(nbrBytes);

                // if (client.Available == 0)
                if (clientState.workingSocket.Available > 0)
                {
                    clientState.workingSocket.BeginReceive(clientState.TempBuffer, 0, clientState.TempBuffer.Length, SocketFlags.None, ReceiveCallback, clientState);
                }
                else
                {
                    TransferDone(clientState);
                    ReceiveData(clientState.workingSocket);
                }

            }
            catch (SocketException e)
            {
                Console.WriteLine("client disconnected");
            }
        }
        private static void Send(Socket client)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] data;
            // Convert the string data to byte data using ASCII encoding.
            string response = "Hello";
           
            
            data = EncodeMessageToSend(response);
             try
            {
                // Begin sending the data to the remote device.
                client.BeginSend(data, 0, data.Length, SocketFlags.None,
                    new AsyncCallback(SendCallback), client);

                Console.WriteLine("Message sent : " + response);
            }
            catch
            {
                client.Close();
                Console.WriteLine("send error");
                AcceptClients(serverSocket);
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            try
            {
                // Retrieve the socket from the state object.

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                //Console.WriteLine("Sent {0} bytes to server.", bytesSent);
                //Receive(client);
            }
            catch (Exception e)
            {
                client.Close();
                Console.WriteLine("sendcallback error");
                AcceptClients(serverSocket);
            }
        }
        private static void ReceiveDone(string message)
        {

            Console.WriteLine(message);

        }
        public static T[] SubArray<T>(T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
        private static string AcceptKey(ref string key)
        {   string longKey = key + guid;
            byte[] hashBytes = ComputeHash(longKey);
            return Convert.ToBase64String(hashBytes);
        }
        static SHA1 sha1 = SHA1CryptoServiceProvider.Create();
        private static byte[] ComputeHash(string str)
        { return sha1.ComputeHash(System.Text.Encoding.ASCII.GetBytes(str));
        }

        private static String DecodeMessage(Byte[] bytes)
        {
            String incomingData = String.Empty;
            Byte secondByte = bytes[1];
            Int32 dataLength = secondByte & 127;
            Int32 indexFirstMask = 2;
            if (dataLength == 126)
                indexFirstMask = 4;
            else if (dataLength == 127)
                indexFirstMask = 10;

            IEnumerable<Byte> keys = bytes.Skip(indexFirstMask).Take(4);
            Int32 indexFirstDataByte = indexFirstMask + 4;

            Byte[] decoded = new Byte[bytes.Length - indexFirstDataByte];
            for (Int32 i = indexFirstDataByte, j = 0; i < bytes.Length; i++, j++)
            {
                decoded[j] = (Byte)(bytes[i] ^ keys.ElementAt(j % 4));
            }

            return incomingData = Encoding.UTF8.GetString(decoded, 0, decoded.Length);
        }


        private static Byte[] EncodeMessageToSend(String message)
        {
            Byte[] response;
            Byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            Byte[] frame = new Byte[10];

            Int32 indexStartRawData = -1;
            Int32 length = bytesRaw.Length;

            frame[0] = (Byte)129;
            if (length <= 125)
            {
                frame[1] = (Byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (Byte)126;
                frame[2] = (Byte)((length >> 8) & 255);
                frame[3] = (Byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (Byte)127;
                frame[2] = (Byte)((length >> 56) & 255);
                frame[3] = (Byte)((length >> 48) & 255);
                frame[4] = (Byte)((length >> 40) & 255);
                frame[5] = (Byte)((length >> 32) & 255);
                frame[6] = (Byte)((length >> 24) & 255);
                frame[7] = (Byte)((length >> 16) & 255);
                frame[8] = (Byte)((length >> 8) & 255);
                frame[9] = (Byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new Byte[indexStartRawData + length];

            Int32 i, reponseIdx = 0;

            //Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }
    }



}

