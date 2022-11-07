using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComNetClient
{
    class Program
    {
        static string readCensored()
        {
            string s = "";
            ConsoleKey key = ConsoleKey.Backspace;
            while(key != ConsoleKey.Enter)
            {
                ConsoleKeyInfo cki = Console.ReadKey(true);
                key = cki.Key;

                if(key == ConsoleKey.Backspace)
                {
                    if(s.Length > 0)
                    {
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        s = s[0..^1];
                    }
                }
                else if(key != ConsoleKey.Enter)
                {
                    s += cki.KeyChar;
                    Console.Write("*");
                }
            }
            Console.WriteLine();
            return s;
        }
        static void Main(string[] args)
        {
            Console.Clear();

            string roomIdStr = "";
            string password0 = "";

            Console.WriteLine("Press SPACE to create a room or press any other key to join one!");
            if(Console.ReadKey(true).Key == ConsoleKey.Spacebar)
            {
                string[] hosts = new string[] { "127.0.0.1", "x444556.ddns.net",
                "this-host-does-not-exist_sfeirfeiieruifij", "host_0", "host_1", "host_2", "host_3", "host_4", "host_5",
                "host_6", "google.com", "bing.com", "host_7", "wikipedia.com", "host_8", "host_9", "host_10", "host_11",
                "host_12", "host_13", "host_14", "host_15", "host_16", "host_17", "host_18", "host_19", "host_20",
                "host_21", "host_22", "host_23", "host_24"};
                string host = null;
                while(String.IsNullOrWhiteSpace(host)) host = SelectNode(ref hosts);
                Console.WriteLine("Selected Node: \"" + host + "\"\n");
                Console.Write("Enter first Password(Join): ");
                password0 = readCensored();
                roomIdStr = Client.CreateRoom(host + (host.Contains(':') ? "" : ":" + ComNetNode.Server.DefaultPort), 8, password0);
                Console.WriteLine("RoomId: " + roomIdStr);
            }
            else
            {
                Console.Write("Enter RoomId: ");
                roomIdStr = Console.ReadLine();
                Console.Write("Enter first Password(Join): ");
                password0 = readCensored();
            }

            Console.Write("Enter Nickname: ");
            string nickname = Console.ReadLine();
            Console.Write("Enter second Password(Encryption): ");
            string password1 = readCensored();

            Client client = new Client(nickname, password0, password1);
            bool success = client.Join(roomIdStr);
            if (success) Console.WriteLine("Joined!");
            else Console.WriteLine("Could not Join!");

            if (success)
            {
                Console.WriteLine("Ping: " + Math.Round(client.Ping(), 3) + " ms");
                Console.WriteLine();

                string currentInput = "";
                string prompt = ">> ";
                Task.Run(() =>
                {
                    while (true)
                    {
                        Client.Packet p = client.Recv(new ComNetNode.Server.PacketType[] { ComNetNode.Server.PacketType.IncomingMessage, 
                            ComNetNode.Server.PacketType.RoomClosing});
                        if(p.Type == ComNetNode.Server.PacketType.IncomingMessage)
                        {
                            byte[] recv = p.Data;
                            ComNetNode.Message msg = new ComNetNode.Message(recv);
                            if (msg.encrypted) msg.content = ComNetNode.Message.Decrypt(msg.content, client.password1);

                            Console.Write("\r" + "".PadRight(Console.WindowWidth - 1, ' ') + "\r");

                            if (msg.isSystemMessage)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine(Encoding.UTF8.GetString(msg.content));
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                            else
                            {
                                Console.WriteLine(Encoding.UTF8.GetString(msg.from) + ": " + Encoding.UTF8.GetString(msg.content));
                            }

                            Console.Write(prompt + currentInput);
                        }
                        else if(p.Type == ComNetNode.Server.PacketType.RoomClosing)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine("\r" + Encoding.UTF8.GetString(p.Data));
                            Console.ForegroundColor = ConsoleColor.White;
                            client.StopRecv();
                            client.client.Close();
                        }
                    }
                });

                Console.Write(prompt);
                while (true)
                {
                    ConsoleKey key = ConsoleKey.Backspace;
                    while (key != ConsoleKey.Enter)
                    {
                        ConsoleKeyInfo cki = Console.ReadKey(true);
                        key = cki.Key;

                        if (key == ConsoleKey.Backspace)
                        {
                            if(currentInput.Length > 0)
                            {
                                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                                Console.Write(" ");
                                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                                currentInput = currentInput.Substring(0, currentInput.Length - 1);
                            }
                        }
                        else if (key == ConsoleKey.Escape) break;
                        else if(cki.KeyChar != '\r')
                        {
                            currentInput += cki.KeyChar;
                            Console.Write(cki.KeyChar);
                        }
                    }
                    if (key == ConsoleKey.Escape)
                    {
                        if(client.client.Connected) client.Leave();
                        break;
                    }
                    string msgText = currentInput;
                    currentInput = "";
                    bool msgSend = client.SendMessage(msgText);
                    if(!msgSend)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine("\nMessage could not be sent!\n");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }
            Console.ReadLine();
        }
        static string SelectNode(ref string[] hosts)
        {
            int winXbefore = Console.WindowWidth;
            int winYbefore = Console.WindowHeight;
            int bufXbefore = Console.BufferWidth;
            int bufYbefore = Console.BufferHeight;

            bool[] hostsAvailable = new bool[hosts.Length];
            for(int i=0; i<hosts.Length; i++)
            {
                int j = i;
                string h = hosts[j];
                Task.Run(() =>
                {
                    try
                    {
                        hostsAvailable[j] = Client.NodePing(h + (h.Contains(':') ? "" : ":" + ComNetNode.Server.DefaultPort)) >= 0;
                    }
                    catch
                    {
                        hostsAvailable[j] = false;
                    }
                });
            }

            int winX = 110;
            int winY = 26;
            int selecLines = winY - 4;
            int selecIndex = 0;
            int pageStart = 0;
            int selecOnPage = 0;

            Console.OutputEncoding = System.Text.Encoding.Unicode;

            Console.CursorVisible = false;
            Console.Clear();
            Console.SetWindowSize(winX, winY);
            Console.SetBufferSize(winX, winY);

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(("Select a Node     Arrow Keys - change Selection    Enter - Select    Escape - Abort    '+' - Add new")
                .PadRight(winX, ' '));
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkBlue;

            Console.WriteLine("\u250E".PadRight(winX - 2, '\u2500') + "\u2512");
            for(int i=0; i<selecLines; i++) Console.WriteLine("\u2503".PadRight(winX - 2, ' ') + "\u2503");
            Console.WriteLine("\u2516".PadRight(winX - 2, '\u2500') + "\u251A");

            ConsoleKeyInfo cki = new ConsoleKeyInfo('_', ConsoleKey.OemMinus, true, false, false);
            while(cki.Key != ConsoleKey.Escape && cki.Key != ConsoleKey.Enter)
            {
                // display
                Console.SetCursorPosition(0, 2);
                for(int i=0; i<selecLines; i++) Console.WriteLine("\u2503".PadRight(winX - 2, ' ') + "\u2503");

                for (int i=0; i<selecLines && pageStart+i<hosts.Length; i++)
                {
                    Console.SetCursorPosition(1, 2 + i);
                    if (i == selecOnPage) Console.BackgroundColor = ConsoleColor.DarkGray;
                    if (!hostsAvailable[pageStart + i]) Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write(hosts[pageStart + i].PadRight(winX - 3, ' '));
                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                    Console.ForegroundColor = ConsoleColor.White;
                }

                cki = Console.ReadKey(true);

                // react to input
                if(cki.Key == ConsoleKey.DownArrow)
                {
                    if(selecIndex + 1 >= hosts.Length)
                    {
                        selecIndex = 0;
                        pageStart = 0;
                        selecOnPage = 0;
                    }
                    else if(selecOnPage + 1 >= selecLines)
                    {
                        pageStart++;
                        selecIndex++;
                    }
                    else
                    {
                        selecIndex++;
                        selecOnPage++;
                    }
                }
                else if (cki.Key == ConsoleKey.UpArrow)
                {
                    if (selecIndex == 0)
                    {
                        selecIndex = hosts.Length - 1;
                        pageStart = selecIndex - selecLines + 1;
                        selecOnPage = selecLines - 1;
                    }
                    else if (selecOnPage == 0)
                    {
                        pageStart--;
                        selecIndex--;
                    }
                    else
                    {
                        selecIndex--;
                        selecOnPage--;
                    }
                }
                else if(cki.Key == ConsoleKey.OemPlus)
                {
                    Console.SetBufferSize(winX, winY + 5);
                    Console.SetWindowSize(winX, winY + 5);

                    Console.SetCursorPosition(0, winY);
                    Console.WriteLine("\u250E".PadRight(winX - 2, '\u2500') + "\u2512");
                    Console.WriteLine("\u2503 Press Enter to confirm.   Press ESCape to cancel.".PadRight(winX - 2, ' ') + "\u2503");
                    Console.WriteLine("\u2503".PadRight(winX - 2, ' ') + "\u2503");
                    Console.WriteLine("\u2516".PadRight(winX - 2, '\u2500') + "\u251A");
                    Console.SetCursorPosition(2, winY + 2);
                    Console.Write("Address: ");
                    Console.CursorVisible = true;

                    string addr = "";
                    ConsoleKey key = ConsoleKey.Backspace;
                    while (key != ConsoleKey.Enter)
                    {
                        ConsoleKeyInfo cki2 = Console.ReadKey(true);
                        key = cki2.Key;

                        if (key == ConsoleKey.Backspace)
                        {
                            if (addr.Length > 0)
                            {
                                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                                Console.Write(" ");
                                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                                addr = addr.Substring(0, addr.Length - 1);
                            }
                        }
                        else if (key == ConsoleKey.Escape) break;
                        else if (cki2.KeyChar != '\r')
                        {
                            addr += cki2.KeyChar;
                            Console.Write(cki2.KeyChar);
                        }
                    }

                    if(key != ConsoleKey.Escape)
                    {
                        Array.Resize(ref hosts, hosts.Length + 1);
                        Array.Resize(ref hostsAvailable, hostsAvailable.Length + 1);
                        hosts[hosts.Length - 1] = addr;
                        int j = hosts.Length - 1;
                        Task.Run(() =>
                        {
                            try
                            {
                                hostsAvailable[j] = Client.NodePing(addr + (addr.Contains(':') ? "" : ":" + ComNetNode.Server.DefaultPort)) >= 0;
                            }
                            catch
                            {
                                hostsAvailable[j] = false;
                            }
                        });
                    }

                    Console.CursorVisible = false;
                    Console.Clear();
                    Console.SetWindowSize(winX, winY);
                    Console.SetBufferSize(winX, winY);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(("Select a Node     Arrow Keys - change Selection    Enter - Select    Escape - Abort    '+' - Add new")
                        .PadRight(winX, ' '));
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.DarkBlue;

                    Console.WriteLine("\u250E".PadRight(winX - 2, '\u2500') + "\u2512");
                    for (int i = 0; i < selecLines; i++) Console.WriteLine("\u2503".PadRight(winX - 2, ' ') + "\u2503");
                    Console.WriteLine("\u2516".PadRight(winX - 2, '\u2500') + "\u251A");
                }
            }

            Console.CursorVisible = true;
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();
            Console.SetWindowSize(winXbefore, winYbefore);
            Console.SetBufferSize(bufXbefore, bufYbefore);
            return cki.Key == ConsoleKey.Enter ? hosts[selecIndex] : null;
        }
    }
    class Client
    {
        public class Packet
        {
            public ComNetNode.Server.PacketType Type;
            public byte[] Data;

            public Packet(byte[] bytes)
            {
                Type = (ComNetNode.Server.PacketType)bytes[0];
                Data = new byte[bytes.Length - 1];
                Array.Copy(bytes, 1, Data, 0, Data.Length);
            }
        }

        public TcpClient client = new TcpClient();
        public string nickname;
        public ulong roomId = 0;
        public byte[] password0; // Room Login
        public byte[] password1; // Message Encryption
        public byte[] password0Hash;

        private List<Packet> PacketQueue = new List<Packet>();
        private bool doRecv = false;
        private bool doRecvActive = false;

        public Client(string nickname, string password0, string password1)
        {
            this.password0 = Encoding.UTF8.GetBytes(password0);
            this.password1 = Encoding.UTF8.GetBytes(password1);
            password0Hash = new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes(password0));
            this.nickname = nickname;
        }

        public bool Join(string roomIdStr)
        {
            int port = (roomIdStr[8] == '-' ? ComNetNode.Server.DefaultPort : int.Parse(roomIdStr.Substring(9, 5)));
            roomId = ulong.Parse(roomIdStr.Substring(roomIdStr[8] == '-' ? 9 : 12));

            client.Connect(new IPAddress(new byte[] { 
                Convert.ToByte(roomIdStr.Substring(6, 2), 16),
                Convert.ToByte(roomIdStr.Substring(4, 2), 16),
                Convert.ToByte(roomIdStr.Substring(2, 2), 16),
                Convert.ToByte(roomIdStr.Substring(0, 2), 16)}), port);
            StartRecv();
            if(Ping() == -1)
            {
                client.Close();
                return false;
            }

            List<byte> lb = new List<byte>();
            lb.Add((byte)ComNetNode.Server.PacketType.Join);
            lb.AddRange(BitConverter.GetBytes(roomId));
            lb.AddRange(Encoding.UTF8.GetBytes(nickname));
            lb.Add(0);
            lb.AddRange(BitConverter.GetBytes(password0Hash.Length));
            lb.AddRange(password0Hash);
            Send(lb.ToArray());

            Packet p = RecvNext();
            return p.Type == ComNetNode.Server.PacketType.ACK;
        }
        public bool SendMessage(string text, bool encrypt=true)
        {
            List<byte> p = new List<byte>();
            p.Add((byte)ComNetNode.Server.PacketType.Send);

            ComNetNode.Message msg = new ComNetNode.Message(Encoding.UTF8.GetBytes(nickname), Encoding.UTF8.GetBytes(text), encrypt);
            if (encrypt) msg.content = ComNetNode.Message.Encrypt(msg.content, password1);

            p.AddRange(msg.GetBytes());
            Send(p.ToArray());

            Packet rp = Recv(new ComNetNode.Server.PacketType[] { ComNetNode.Server.PacketType.ACK, ComNetNode.Server.PacketType.Rejected, 
                ComNetNode.Server.PacketType.ERROR });
            return rp.Type == ComNetNode.Server.PacketType.ACK;
        }
        public void Leave()
        {
            Send(new byte[] { (byte)ComNetNode.Server.PacketType.Leave });
            StopRecv();
            client.Close();
        }

        public bool Send(byte[] bytes)
        {
            //Console.WriteLine("Send( " + BitConverter.ToString(bytes).Replace("-", " ") + " )");
            if(client.Connected)
            {
                client.GetStream().Write(BitConverter.GetBytes(bytes.Length));
                //Console.WriteLine("  sent length");
                client.GetStream().Write(bytes);
                //Console.WriteLine("  sent data");
                return true;
            }
            //Console.WriteLine("  could not send bytes!");
            return false;
        }
        public void StartRecv()
        {
            doRecv = true;
            Task.Run(() =>
            {
                doRecvActive = true;
                while (client.Connected && doRecv)
                {
                    List<byte> bytes = new List<byte>();
                    while ((bytes.Count < 4 || bytes.Count - 4 < BitConverter.ToInt32(bytes.ToArray(), 0)) && doRecv)
                    {
                        while (client.Available == 0 && doRecv) Thread.Sleep(1);
                        if (doRecv)
                        {
                            int bc = 4;
                            if (bytes.Count >= 4 && bytes.Count > BitConverter.ToInt32(bytes.ToArray(), 0)) 
                                bc = BitConverter.ToInt32(bytes.ToArray(), 0) - (bytes.Count - 4);
                            byte[] b = new byte[client.Available > bc ? bc : client.Available];
                            client.GetStream().Read(b, 0, b.Length);
                            bytes.AddRange(b);
                        }
                        //Console.WriteLine("  Received bytes [ " + BitConverter.ToString(b).Replace("-", " ") + " ]");
                    }
                    if(doRecv) PacketQueue.Add(new Packet(bytes.Skip(4).ToArray()));
                }
                doRecvActive = false;
            });
            while (!doRecvActive) Thread.Sleep(1);
        }
        public void StopRecv()
        {
            doRecv = false;
            while (doRecvActive) Thread.Sleep(1);
        }
        public Packet RecvNext()
        {
            while (PacketQueue.Count == 0) Thread.Sleep(1);
            Packet p = PacketQueue[0];
            PacketQueue.RemoveAt(0);
            return p;
            //Console.WriteLine("Receiving...");
            /*if(client.Connected)
            {
                List<byte> bytes = new List<byte>();
                while(bytes.Count < 4 || bytes.Count - 4 < BitConverter.ToInt32(bytes.ToArray(), 0))
                {
                    while (client.Available == 0) Thread.Sleep(1);
                    byte[] b = new byte[client.Available];
                    client.GetStream().Read(b, 0, b.Length);
                    bytes.AddRange(b);
                    //Console.WriteLine("  Received bytes [ " + BitConverter.ToString(b).Replace("-", " ") + " ]");
                }
                return bytes.Skip(4).ToArray();
            }*/
            //Console.WriteLine("  could not receive bytes!");
            //return null;
        }
        public Packet Recv(ComNetNode.Server.PacketType type)
        {
            Packet p = null;
            while(p == null)
            {
                for(int i=0; i<PacketQueue.Count; i++)
                {
                    if(PacketQueue[i].Type == type)
                    {
                        p = PacketQueue[i];
                        PacketQueue.RemoveAt(i);
                        i = PacketQueue.Count + 1;
                    }
                }
                if (p == null) Thread.Sleep(1);
            }
            return p;
        }
        public Packet Recv(ComNetNode.Server.PacketType[] types)
        {
            Packet p = null;
            while (p == null)
            {
                for (int i = 0; i < PacketQueue.Count; i++)
                {
                    for(int j=0; j<types.Length; j++)
                    {
                        if (PacketQueue[i].Type == types[j])
                        {
                            p = PacketQueue[i];
                            PacketQueue.RemoveAt(i);
                            i = PacketQueue.Count + 1;
                            j = types.Length;
                        }
                    }
                }
                if (p == null) Thread.Sleep(1);
            }
            return p;
        }

        public double Ping()
        {
            if (!client.Connected) return -1;
            try
            {
                NetworkStream stream = client.GetStream();
                DateTime start = DateTime.Now;
                Send(new byte[] { (byte)ComNetNode.Server.PacketType.Ping });
                Packet p = Recv(ComNetNode.Server.PacketType.Pong);

                return (DateTime.Now - start).TotalMilliseconds;
            }
            catch
            {
                return -1;
            }
        }
        public static double NodePing(string hostAndPort)
        {
            TcpClient client = new TcpClient();
            client.Connect(hostAndPort.Split(':')[0], int.Parse(hostAndPort.Split(':')[1]));
            try
            {
                NetworkStream stream = client.GetStream();
                DateTime start = DateTime.Now;
                stream.Write(BitConverter.GetBytes((int)1));
                stream.Write(new byte[] { (byte)ComNetNode.Server.PacketType.Ping });
                List<byte> bytes = new List<byte>();
                while ((bytes.Count < 4 || bytes.Count - 4 < BitConverter.ToInt32(bytes.ToArray(), 0)))
                {
                    while (client.Available == 0) Thread.Sleep(1);
                    int bc = 4;
                    if (bytes.Count >= 4 && bytes.Count > BitConverter.ToInt32(bytes.ToArray(), 0))
                        bc = BitConverter.ToInt32(bytes.ToArray(), 0) - (bytes.Count - 4);
                    byte[] b = new byte[client.Available > bc ? bc : client.Available];
                    client.GetStream().Read(b, 0, b.Length);
                    bytes.AddRange(b);
                }

                if (bytes[4] != (byte)ComNetNode.Server.PacketType.Pong) return -1;

                return (DateTime.Now - start).TotalMilliseconds;
            }
            catch
            {
                return -1;
            }
        }
        public static string CreateRoom(string hostAndPort, byte maxUsers, string password0)
        {
            List<byte> p = new List<byte>();
            p.Add((byte)ComNetNode.Server.PacketType.CreateRoom);
            p.Add(maxUsers);
            p.AddRange(getHashSha256(password0));

            TcpClient client = new TcpClient();
            client.Connect(hostAndPort.Split(':')[0], int.Parse(hostAndPort.Split(':')[1]));
            try
            {
                NetworkStream stream = client.GetStream();
                stream.Write(p.ToArray());
                List<byte> bytes = new List<byte>();
                while ((bytes.Count < 4 || bytes.Count - 4 < BitConverter.ToInt32(bytes.ToArray(), 0)))
                {
                    while (client.Available == 0) Thread.Sleep(1);
                    int bc = 4;
                    if (bytes.Count >= 4 && bytes.Count > BitConverter.ToInt32(bytes.ToArray(), 0))
                        bc = BitConverter.ToInt32(bytes.ToArray(), 0) - (bytes.Count - 4);
                    byte[] b = new byte[client.Available > bc ? bc : client.Available];
                    client.GetStream().Read(b, 0, b.Length);
                    bytes.AddRange(b);
                }

                if (bytes[4] != (byte)ComNetNode.Server.PacketType.RoomCreated) return null;

                return Encoding.UTF8.GetString(bytes.Skip(5).ToArray());
            }
            catch
            {
                return null;
            }
        }

        public static byte[] getHashSha256(string text)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);
            return hash;
        }
    }
}
