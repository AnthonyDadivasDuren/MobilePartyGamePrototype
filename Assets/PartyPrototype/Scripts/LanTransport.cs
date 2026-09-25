using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PartyPrototype
{
    // Length-prefixed UTF-8. Worker threads never call Unity APIs.
    public sealed class LanTransport : IDisposable
    {
        public const int Port = 7777;
        const int MaxPacket = 65536;
        public sealed class Event { public string kind, data; public int peer; }
        sealed class Peer
        {
            public TcpClient client;
            public readonly BlockingCollection<string> outgoing = new BlockingCollection<string>(64);
        }
        readonly ConcurrentQueue<Event> events = new ConcurrentQueue<Event>();
        readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
        readonly object gate = new object();
        TcpListener listener;
        volatile bool stopped;
        int nextId;
        public bool Poll(out Event item) => events.TryDequeue(out item);
        void Emit(string kind, int id, string data = null)
        { if (!stopped) events.Enqueue(new Event { kind = kind, peer = id, data = data }); }
        static void Run(ThreadStart action) => new Thread(action) { IsBackground = true }.Start();
        public void Host()
        {
            listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Run(() =>
            {
                try
                {
                    while (!stopped)
                    {
                        var client = listener.AcceptTcpClient();
                        lock (gate)
                        {
                            if (stopped || peers.Count >= 7) { client.Close(); continue; }
                            Attach(++nextId, client);
                        }
                    }
                }
                catch (Exception e) { if (!stopped) Emit("error", 0, e.Message); }
            });
        }
        public void Connect(string address)
        {
            Run(() =>
            {
                var client = new TcpClient();
                try
                {
                    var pending = client.BeginConnect(address, Port, null, null);
                    using (pending.AsyncWaitHandle)
                        if (!pending.AsyncWaitHandle.WaitOne(5000)) throw new IOException("Connection timed out. Check the host IP and Wi-Fi.");
                    client.EndConnect(pending);
                    lock (gate)
                    { if (stopped) client.Close(); else Attach(0, client); }
                }
                catch (Exception e) { client.Close(); Emit("error", 0, e.Message); }
            });
        }
        void Attach(int id, TcpClient client)
        {
            client.NoDelay = true; client.SendTimeout = 5000; client.ReceiveTimeout = 15000;
            var peer = new Peer { client = client };
            peers.Add(id, peer); Emit("connected", id);
            Run(() =>
            {
                try
                {
                    var stream = client.GetStream();
                    foreach (string text in peer.outgoing.GetConsumingEnumerable())
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(text);
                        if (bytes.Length > MaxPacket) throw new IOException("Packet too large.");
                        var header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
                        stream.Write(header, 0, 4); stream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception) { }
                finally { Drop(id); }
            });
            Run(() =>
            {
                try
                {
                    var stream = client.GetStream();
                    var header = new byte[4];
                    while (!stopped)
                    {
                        ReadFully(stream, header);
                        int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
                        if (length < 1 || length > MaxPacket) throw new IOException("Invalid packet length.");
                        var bytes = new byte[length]; ReadFully(stream, bytes);
                        if (events.Count > 256) throw new IOException("Too many pending messages.");
                        Emit("message", id, Encoding.UTF8.GetString(bytes));
                    }
                }
                catch (Exception) { }
                finally { Drop(id); }
            });
        }
        static void ReadFully(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int count = stream.Read(buffer, offset, buffer.Length - offset);
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
        }
        public void Send(int id, string data)
        {
            bool failed = false;
            lock (gate)
            {
                if (peers.TryGetValue(id, out var peer))
                    failed = !peer.outgoing.TryAdd(data);
            }
            if (failed) Drop(id);
        }
        public void Drop(int id)
        {
            lock (gate)
            {
                if (!peers.TryGetValue(id, out var peer)) return;
                peers.Remove(id); peer.outgoing.CompleteAdding(); peer.client.Close();
            }
            Emit("disconnected", id);
        }
        public void Dispose()
        {
            stopped = true; listener?.Stop();
            lock (gate)
            {
                foreach (var peer in peers.Values) { peer.outgoing.CompleteAdding(); peer.client.Close(); }
                peers.Clear();
            }
        }
    }
}
