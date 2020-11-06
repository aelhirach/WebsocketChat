using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace tcpServer
{
    public class StateObject
    {
        public Socket workingSocket;
        public byte[] TempBuffer = new byte[1024];
        // le buffer conplèt encapsuler 
        private List<byte> buffer = new List<byte>();
        public List<byte> Buffer
        {
            get { return buffer; }
        }

        // public String Temp=""; 

        public void AddTempBufferToBuffer(int nbrBytes)
        {
            for (int i = 0; i < nbrBytes; i++)
                Buffer.Add(TempBuffer[i]);

        }
    }
}

