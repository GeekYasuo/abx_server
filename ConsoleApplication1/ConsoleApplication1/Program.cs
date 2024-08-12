using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net;


class Program
{
    static void Main(string[] args)
    {
        try
        {
            TcpClient client = new TcpClient("localhost", 3000);
            NetworkStream stream = client.GetStream();
            Console.WriteLine("Connected to ABX server...");

            byte[] requestPayload = new byte[] { 1 }; // Stream All Packets
            stream.Write(requestPayload, 0, requestPayload.Length);

            List<Packet> packets = new List<Packet>();
            byte[] buffer = new byte[17]; // Each packet is 17 bytes long
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                Packet packet = ParsePacket(buffer);
                packets.Add(packet);
            }

            List<int> missingSequences = FindMissingSequences(packets);

            foreach (int seq in missingSequences)
            {
                byte[] resendRequest = new byte[] { 2, (byte)seq };
                stream.Write(resendRequest, 0, resendRequest.Length);

                stream.Read(buffer, 0, buffer.Length); // Read the missing packet
                Packet missingPacket = ParsePacket(buffer);
                packets.Add(missingPacket);
            }

            string json = JsonConvert.SerializeObject(packets, Formatting.Indented);
            File.WriteAllText("output.json", json);

            Console.WriteLine("JSON file created successfully.");
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e.Message);
        }
    }

    static Packet ParsePacket(byte[] buffer)
    {
        string symbol = Encoding.ASCII.GetString(buffer, 0, 4);
        char buySellIndicator = (char)buffer[4];
        int quantity = BitConverter.ToInt32(buffer, 5);
        int price = BitConverter.ToInt32(buffer, 9);
        int sequence = BitConverter.ToInt32(buffer, 13);

        return new Packet
        {
            Symbol = symbol,
            BuySellIndicator = buySellIndicator,
            Quantity = IPAddress.NetworkToHostOrder(quantity),
            Price = IPAddress.NetworkToHostOrder(price),
            Sequence = IPAddress.NetworkToHostOrder(sequence)
        };
    }

    static List<int> FindMissingSequences(List<Packet> packets)
    {
        List<int> missingSequences = new List<int>();
        packets.Sort((x, y) => x.Sequence.CompareTo(y.Sequence));

        for (int i = 0; i < packets.Count - 1; i++)
        {
            if (packets[i + 1].Sequence != packets[i].Sequence + 1)
            {
                missingSequences.Add(packets[i].Sequence + 1);
            }
        }

        return missingSequences;
    }
}

class Packet
{
    public string Symbol { get; set; }
    public char BuySellIndicator { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int Sequence { get; set; }
}
