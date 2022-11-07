using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComNetNode
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!Directory.Exists("logs\\")) Directory.CreateDirectory("logs\\");
            Out.Run("logs\\log_" + DateTime.Now.Ticks + ".txt");

            Console.Clear();
            Out.WriteLine("ComNet Node EarlyDev");
            Out.WriteLine("Developed by x444556");
            Out.WriteLine();

            Server server;
            if (File.Exists("server.json"))
            {
                server = JsonConvert.DeserializeObject<Server>(File.ReadAllText("server.json"));
            }
            else
            {
                server = new Server();
            }
            server.Addr = "127.0.0.1:" + Server.DefaultPort;

            Console.WriteLine("Test Room: " + server.CreateRoom(8, new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes("1234")), true));

            Task.Run(() =>
            {
                int c = 0;
                while (true)
                {
                    Console.Title = "Received: " + Storage(server.BytesRecieved) + "    Sent: " + Storage(server.BytesSend);
                    if (c % (60 * 30) == 0) Out.WriteLine(Console.Title); // every 30 minutes
                    if (c % (60 *  5) == 0) File.WriteAllText("server.json", JsonConvert.SerializeObject(server)); // backup every 5 min
                    if (c % (60 * 15) == 0) Out.WriteLine("Closed " + server.CloseEmptyRooms(false) + " empty rooms!"); // every 15 min
                    c++;
                    Thread.Sleep(1000);
                }
            });
            Task.Run(() =>
            {
                while (true)
                {
                    // Commands:
                    // backup
                    // close

                    string input = Console.ReadLine();
                    if (input.StartsWith("backup"))
                    {
                        File.WriteAllText("server.json", JsonConvert.SerializeObject(server));
                        Out.WriteLine("Manual backup: success!");
                    }
                    else if (input.StartsWith("close"))
                    {
                        if (input.EndsWith('*'))
                        {
                            Out.WriteLine("[CMD] Closed " + server.CloseAllRooms() + " rooms!");
                        }
                        else
                        {
                            Out.WriteLine("[CMD] closed " + server.CloseEmptyRooms(input.EndsWith("f")) + " empty rooms!");
                        }
                    }
                }
            });

            if (args.Length >= 1 && int.TryParse(args[0], out int port)) server.Run(port);
            else server.Run(Server.DefaultPort);
        }
        public static string Storage(ulong bytes)
        {
            if (bytes >= 1000000000000) return Math.Round(bytes / 1000000000000.0, 2).ToString("0.00") + " TB";
            else if (bytes >= 1000000000) return Math.Round(bytes / 1000000000.0, 2).ToString("0.00") + " GB";
            else if (bytes >= 1000000) return Math.Round(bytes / 1000000.0, 2).ToString("0.00") + " MB";
            else if (bytes >= 1000) return Math.Round(bytes / 1000.0, 2).ToString("0.00") + " KB";
            else return bytes + "  B";
        }
    }
    public static class Out
    {
        public static List<string> Lines = new List<string>();

        public static void Run()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    while (Lines.Count > 0)
                    {
                        Console.WriteLine(Lines[0]);
                        Lines.RemoveAt(0);
                    }
                    Thread.Sleep(100);
                }
            });
        }
        public static void Run(string logPath)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    while (Lines.Count > 0)
                    {
                        Console.WriteLine(Lines[0]);
                        File.AppendAllText(logPath, Lines[0] + "\n");
                        Lines.RemoveAt(0);
                    }
                    Thread.Sleep(100);
                }
            });
        }

        public static void WriteLine(string line = "")
        {
            // Console.WriteLine(line);
            Lines.Add(line);
        }
        public static void Wait()
        {
            while (Lines.Count > 0) Thread.Sleep(1);
        }
    }
    public class Message
    {
        public byte[] from;
        public byte[] content;
        public bool encrypted;
        public bool isSystemMessage;

        public Message(byte[] systemMessage, bool _b=true)
        {
            from = new byte[0];
            content = systemMessage;
            encrypted = false;
            isSystemMessage = true;
        }
        public Message(byte[] from, byte[] content, bool encrypted)
        {
            this.from = from;
            this.content = content;
            this.encrypted = encrypted;
        }
        public Message(byte[] bytes)
        {
            encrypted = bytes[0] != 0;
            isSystemMessage = bytes[1] != 0;
            from = new byte[BitConverter.ToInt32(bytes, 2)];
            content = new byte[BitConverter.ToInt32(bytes, 6)];
            for (int i = 0; i < from.Length; i++) from[i] = bytes[10 + i];
            for (int i = 0; i < content.Length; i++) content[i] = bytes[10 + from.Length + i];
        }

        public byte[] GetBytes()
        {
            byte[] bytes = new byte[2 + from.Length + 4 + content.Length + 4];
            bytes[0] = (byte)(encrypted ? 1 : 0);
            bytes[1] = (byte)(isSystemMessage ? 1 : 0);
            Array.Copy(BitConverter.GetBytes(from.Length), 0, bytes, 2, 4);
            Array.Copy(BitConverter.GetBytes(content.Length), 0, bytes, 6, 4);
            Array.Copy(from, 0, bytes, 10, from.Length);
            Array.Copy(content, 0, bytes, 10 + from.Length, content.Length);
            return bytes;
        }

        public static byte[] Encrypt(byte[] data, byte[] key)
        {
            byte[] r = new byte[data.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (byte)(data[i] ^ key[i % key.Length]);
            return r;
        }
        public static byte[] Decrypt(byte[] data, byte[] key)
        {
            byte[] r = new byte[data.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (byte)(data[i] ^ key[i % key.Length]);
            return r;
        }
    }
    public class Member
    {
        public string nickname;
        public NetworkStream stream;

        public Member(string nickname, NetworkStream stream)
        {
            this.nickname = nickname;
            this.stream = stream;
        }
    }
    public class Room
    {
        public ulong ID = 0;
        public int MaxUsers = 4;
        public bool keepIfEmpty = false;

        public byte[] JoinPasswordHash;

        [NonSerialized]
        public List<Member> members = new List<Member>();

        public Room(ulong id, int maxUsers, byte[] joinPwHash)
        {
            ID = id;
            MaxUsers = maxUsers;
            JoinPasswordHash = joinPwHash;
        }

        public void SendMessage(Message msg)
        {
            for(int i=0; i<members.Count; i++)
            {
                try
                {
                    byte[] bytes = msg.GetBytes();
                    members[i].stream.Write(BitConverter.GetBytes(bytes.Length + 1));
                    members[i].stream.WriteByte((byte)Server.PacketType.IncomingMessage);
                    members[i].stream.Write(bytes);
                }
                catch(Exception e)
                {
                    //Console.WriteLine(e);
                    //Console.WriteLine("Removing \"" + members[i].nickname + "\"");
                    members.RemoveAt(i);
                    i--;
                }
            }
        }
        public void Close()
        {
            MaxUsers = 0;
            keepIfEmpty = false;
            JoinPasswordHash = new byte[0];
            byte[] bytes = Encoding.UTF8.GetBytes("This room has been closed!");
            while(members.Count > 0)
            {
                try
                {
                    members[0].stream.Write(BitConverter.GetBytes(bytes.Length + 1));
                    members[0].stream.WriteByte((byte)Server.PacketType.RoomClosing);
                    members[0].stream.Write(bytes);
                    members.RemoveAt(0);
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e);
                }
            }
        }
        public bool Join(Member member, byte[] pwHash)
        {
            if (members.Count >= MaxUsers) return false;

            bool pwHashEqual = true;
            if (pwHash.Length != JoinPasswordHash.Length) pwHashEqual = false;
            else
            {
                for (int i = 0; i < pwHash.Length; i++)
                {
                    if(pwHash[i] != JoinPasswordHash[i])
                    {
                        pwHashEqual = false;
                        i = pwHash.Length;
                    }
                }
            }
            if (!pwHashEqual) return false;

            bool nicknameAvailable = true;
            for(int i=0; i<members.Count; i++)
            {
                if(members[i].nickname.ToLower() == member.nickname.ToLower())
                {
                    nicknameAvailable = false;
                    i = members.Count;
                }
            }
            if (!nicknameAvailable) return false;

            SendMessage(new Message(Encoding.UTF8.GetBytes(member.nickname + " joined the room!"), true));
            members.Add(member);
            return true;
        }
        public void Leave(Member member)
        {
            bool removed = false;
            for(int i=0; i<members.Count; i++)
            {
                if(members[i] == member)
                {
                    members.RemoveAt(i);
                    i = members.Count;
                    removed = true;
                }
            }
            if(removed) SendMessage(new Message(Encoding.UTF8.GetBytes(member.nickname + " left the room!"), true));
        }
    }
    public class Server
    {
        public const int DefaultPort = 5000;
        public const int PwHashLenBytes = 32;

        public string Addr = "127.0.0.1:5000";
        public ulong BytesRecieved = 0;
        public ulong BytesSend = 0;
        public int BufferSize = 32 * 1024; // 32 KiB
        public TimeSpan Timeout = new TimeSpan(0, 0, 0, 5, 0);

        public List<Room> Rooms = new List<Room>();
        public ulong NextRoomId = 0;

        [NonSerialized]
        private ManualResetEvent tcpClientConnected = new ManualResetEvent(false);

        public enum PacketType
        {
            UNKNOWN = 0x00,

            Ping = 0x01,
            Join = 0x02,
            Leave = 0x03,
            Send = 0x04,
            CreateRoom = 0x05,

            Pong = 0x10,
            Timeout = 0x20,
            ACK = 0x30,
            Rejected = 0x40,
            IncomingMessage = 0x50,
            RoomClosing = 0x60,
            RoomCreated = 0x70,

            NotAvailable = 0xF0,

            ERROR = 0xFF
        }

        public Server()
        {
            Addr = (new IPEndPoint(IPAddress.Any, DefaultPort)).ToString() + ":" + DefaultPort;
        }

        public void Run(int port = 5000)
        {
            Out.WriteLine("Server Buffer: " + Program.Storage((ulong)BufferSize));

            TcpListener listener = new TcpListener(IPAddress.Any, port);
            Addr = ((IPEndPoint)listener.LocalEndpoint).Address.ToString() + ":" + ((IPEndPoint)listener.LocalEndpoint).Port;

            listener.Start();

            while (true)
            {
                // Set the event to nonsignaled state.
                tcpClientConnected.Reset();

                // Accept the connection. BeginAcceptSocket() creates the accepted socket.
                listener.BeginAcceptTcpClient(
                    new AsyncCallback(DoAcceptTcpClientCallback),
                    listener);

                // Wait until a connection is made and processed before continuing.
                tcpClientConnected.WaitOne();
            }
        }
        // Process the client connection.
        private void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            // Get the listener that handles the client request.
            TcpListener listener = (TcpListener)ar.AsyncState;

            // End the operation
            TcpClient client = listener.EndAcceptTcpClient(ar);

            // Signal the calling thread to continue.
            tcpClientConnected.Set();

            Room room = null;
            Member member = null;
            while (client.Connected)
            {
                List<byte> recvBytes = new List<byte>();
                while (recvBytes.Count < 4 || recvBytes.Count - 4 < BitConverter.ToInt32(recvBytes.ToArray(), 0))
                {
                    while (client.Available == 0) Thread.Sleep(1);
                    byte[] b = new byte[client.Available];
                    client.GetStream().Read(b, 0, b.Length);
                    recvBytes.AddRange(b);
                }
                BytesRecieved += (ulong)recvBytes.Count;
                byte[] recieved = recvBytes.Skip(4).ToArray();

                List<byte> response = new List<byte>();
                // proccess traffic
                if(recieved.Length > 0)
                {
                    // Console.WriteLine("Received: " + BitConverter.ToString(recieved).Replace("-", " "));

                    if (recieved[0] == (byte)PacketType.Ping)
                    {
                        response.Add((byte)PacketType.Pong);
                        if (response.Count == 0) response.Add((byte)PacketType.ERROR);
                        client.GetStream().Write(BitConverter.GetBytes(response.Count));
                        client.GetStream().Write(response.ToArray());
                        BytesSend += (ulong)response.Count + 4;
                    }
                    else if (recieved[0] == (byte)PacketType.Join)
                    {
                        ulong roomId = BitConverter.ToUInt64(recieved, 1);
                        List<byte> nicknameBytes = new List<byte>();
                        for (int i = 9; i < recieved.Length && recieved[i] != 0; i++)
                        {
                            nicknameBytes.Add(recieved[i]);
                        }
                        string nickname = Encoding.UTF8.GetString(nicknameBytes.ToArray());
                        int pw0hashLen = BitConverter.ToInt32(recieved, 10 + nicknameBytes.Count);
                        byte[] pw0Hash = new byte[pw0hashLen];
                        Array.Copy(recieved, 14 + nicknameBytes.Count, pw0Hash, 0, pw0hashLen);

                        member = new Member(nickname, client.GetStream());
                        room = null;
                        for (int i = 0; i < Rooms.Count; i++)
                        {
                            if (Rooms[i].ID == roomId)
                            {
                                room = Rooms[i];
                                i = Rooms.Count;
                            }
                        }
                        if (room == null) response.Add((byte)PacketType.Rejected);
                        else
                        {
                            bool success = room.Join(member, pw0Hash);
                            if (!success) response.Add((byte)PacketType.Rejected);
                            else response.Add((byte)PacketType.ACK);
                        }

                        if (response.Count == 0) response.Add((byte)PacketType.ERROR);
                        client.GetStream().Write(BitConverter.GetBytes(response.Count));
                        client.GetStream().Write(response.ToArray());
                        BytesSend += (ulong)response.Count + 4;
                    }
                    else if (recieved[0] == (byte)PacketType.Leave)
                    {
                        if (room == null || member == null) response.Add((byte)PacketType.Rejected);
                        else
                        {
                            room.Leave(member);
                            room = null;
                            member = null;
                            response.Add((byte)PacketType.ACK);

                            client.Close();
                            break;
                        }
                    }
                    else if (recieved[0] == (byte)PacketType.Send)
                    {
                        if (room == null || member == null) response.Add((byte)PacketType.Rejected);
                        else
                        {
                            //Message msg = new Message(Encoding.UTF8.GetBytes(member.nickname), recieved.Skip(1).ToArray(), false);
                            Message msg = new Message(recieved.Skip(1).ToArray());
                            msg.from = Encoding.UTF8.GetBytes(member.nickname);
                            msg.isSystemMessage = false;

                            client.GetStream().Write(BitConverter.GetBytes(1));
                            client.GetStream().WriteByte((byte)PacketType.ACK);
                            BytesSend += 5;

                            room.SendMessage(msg);
                        }
                    }
                    else if (recieved[0] == (byte)PacketType.CreateRoom)
                    {
                        string s = CreateRoom(recieved[1], recieved.Skip(2).Take(PwHashLenBytes).ToArray(), true);
                        client.GetStream().Write(BitConverter.GetBytes(1 + s.Length));
                        client.GetStream().WriteByte((byte)PacketType.RoomCreated);
                        client.GetStream().Write(Encoding.UTF8.GetBytes(s), 0, s.Length);
                        BytesSend += 5 + (ulong)s.Length;
                    }
                } 
            }

        }
        public string CreateRoom(int maxUsers, byte[] joinPwHash, bool keepIfEmtpy=false)
        {
            Room room = new Room(NextRoomId, maxUsers, joinPwHash);
            Rooms.Add(room);
            Rooms[Rooms.Count - 1].keepIfEmpty = keepIfEmtpy;
            NextRoomId++;
            string[] ipsplits = Addr.Split(':')[0].Split('.');
            /*string ridstr = ipsplits[0].PadLeft(3, '0') + ipsplits[1].PadLeft(3, '0') + ipsplits[2].PadLeft(3, '0') + 
                ipsplits[3].PadLeft(3, '0') + Addr.Split(':')[1].PadLeft(5, '0') + (NextRoomId - 1);*/

            string ridstr = BitConverter.ToString(IPAddress.Parse(Addr.Split(':')[0]).GetAddressBytes().Reverse().ToArray())
                .Replace("-", "") + (Addr.EndsWith(DefaultPort.ToString()) ? "-" : Addr.Split(':')[1].PadLeft(5, '0')) + (NextRoomId - 1);
            return ridstr;
        }
        public int CloseEmptyRooms(bool ignoreKeepOpen = false)
        {
            int roomsClosed = 0;
            for(int i=0; i<Rooms.Count; i++)
            {
                if(Rooms[i].members.Count == 0 && (!Rooms[i].keepIfEmpty || ignoreKeepOpen))
                {
                    Rooms[i].Close();
                    Rooms.RemoveAt(i);
                    i--;
                    roomsClosed++;
                }
            }
            return roomsClosed;
        }
        public int CloseAllRooms()
        {
            int roomsClosed = 0;
            while(Rooms.Count > 0)
            {
                Rooms[0].Close();
                Rooms.RemoveAt(0);
                roomsClosed++;
            }
            return roomsClosed;
        }
    }
}
