using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Xml;

class MsmqUtilities
{
    public static string GetFullPath(MsmqAddress value)
    {
        IPAddress ipAddress;
        if (IPAddress.TryParse(value.Machine, out ipAddress))
        {
            return PREFIX_TCP + GetFullPathWithoutPrefix(value);
        }

        return PREFIX + GetFullPathWithoutPrefix(value);
    }

    public static string GetFullPath(string queue)
    {
        return PREFIX + GetFullPathWithoutPrefix(queue, Environment.MachineName);
    }

    public static string GetReturnAddress(string replyToString, string destinationMachine)
    {
        var replyToAddress = MsmqAddress.Parse(replyToString);
        IPAddress targetIpAddress;

        //see if the target is an IP address, if so, get our own local ip address
        if (IPAddress.TryParse(destinationMachine, out targetIpAddress))
        {
            if (string.IsNullOrEmpty(localIp))
            {
                localIp = LocalIpAddress(targetIpAddress);
            }

            return PREFIX_TCP + localIp + PRIVATE + replyToAddress.Queue;
        }

        return PREFIX + GetFullPathWithoutPrefix(replyToAddress);
    }

    static string LocalIpAddress(IPAddress targetIpAddress)
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        var availableAddresses =
            networkInterfaces.Where(
                ni =>
                    ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses).ToList();

        var firstWithMatchingFamily =
            availableAddresses.FirstOrDefault(a => a.Address.AddressFamily == targetIpAddress.AddressFamily);

        if (firstWithMatchingFamily != null)
        {
            return firstWithMatchingFamily.Address.ToString();
        }

        var fallbackToDifferentFamily = availableAddresses.FirstOrDefault();

        if (fallbackToDifferentFamily != null)
        {
            return fallbackToDifferentFamily.Address.ToString();
        }

        return "127.0.0.1";
    }


    static MsmqAddress GetIndependentAddressForQueue(MessageQueue q)
    {
        var arr = q.FormatName.Split('\\');
        var queueName = arr[arr.Length - 1];

        var directPrefixIndex = arr[0].IndexOf(DIRECTPREFIX);
        if (directPrefixIndex >= 0)
        {
            return new MsmqAddress(queueName, arr[0].Substring(directPrefixIndex + DIRECTPREFIX.Length));
        }

        var tcpPrefixIndex = arr[0].IndexOf(DIRECTPREFIX_TCP);
        if (tcpPrefixIndex >= 0)
        {
            return new MsmqAddress(queueName, arr[0].Substring(tcpPrefixIndex + DIRECTPREFIX_TCP.Length));
        }

        try
        {
            // the pessimistic approach failed, try the optimistic approach
            arr = q.QueueName.Split('\\');
            queueName = arr[arr.Length - 1];
            return new MsmqAddress(queueName, q.MachineName);
        }
        catch
        {
            throw new Exception("Could not translate format name to independent name: " + q.FormatName);
        }
    }

    public static Dictionary<string, string> ExtractHeaders(Message msmqMessage)
    {
        var headers = DeserializeMessageHeaders(msmqMessage);

        //note: we can drop this line when we no longer support interop btw v3 + v4
        if (msmqMessage.ResponseQueue != null)
        {
            headers[Headers.ReplyToAddress] = GetIndependentAddressForQueue(msmqMessage.ResponseQueue).ToString();
        }

        if (Enum.IsDefined(typeof(MessageIntentEnum), msmqMessage.AppSpecific))
        {
            headers[Headers.MessageIntent] = ((MessageIntentEnum)msmqMessage.AppSpecific).ToString();
        }

        headers[Headers.CorrelationId] = GetCorrelationId(msmqMessage, headers);

        return headers;
    }

    static string GetCorrelationId(Message message, Dictionary<string, string> headers)
    {
        string correlationId;

        if (headers.TryGetValue(Headers.CorrelationId, out correlationId))
        {
            return correlationId;
        }

        if (message.CorrelationId == "00000000-0000-0000-0000-000000000000\\0")
        {
            return null;
        }

        //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end that the sender added to make it compatible
        //The replace can be removed in v5 since only v3 messages will need this
        return message.CorrelationId.Replace("\\0", String.Empty);
    }

    static Dictionary<string, string> DeserializeMessageHeaders(Message m)
    {
        var result = new Dictionary<string, string>();

        if (m.Extension.Length == 0)
        {
            return result;
        }

        //This is to make us compatible with v3 messages that are affected by this bug:
        //http://stackoverflow.com/questions/3779690/xml-serialization-appending-the-0-backslash-0-or-null-character
        var extension = Encoding.UTF8.GetString(m.Extension).TrimEnd('\0');
        object o;
        using (var stream = new StringReader(extension))
        {
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                CheckCharacters = false
            }))
            {
                o = headerSerializer.Deserialize(reader);
            }
        }

        foreach (var pair in (List<HeaderInfo>)o)
        {
            if (pair.Key != null)
            {
                result.Add(pair.Key, pair.Value);
            }
        }

        return result;
    }

    public static string GetFullPathWithoutPrefix(MsmqAddress address)
    {
        return GetFullPathWithoutPrefix(address.Queue, address.Machine);
    }

    public static string GetFullPathWithoutPrefix(string queue, string machine)
    {
        return machine + PRIVATE + queue;
    }

    const string DIRECTPREFIX = "DIRECT=OS:";
    const string DIRECTPREFIX_TCP = "DIRECT=TCP:";
    const string PREFIX_TCP = "FormatName:" + DIRECTPREFIX_TCP;
    const string PREFIX = "FormatName:" + DIRECTPREFIX;
    internal const string PRIVATE = "\\private$\\";
    static string localIp;
    static System.Xml.Serialization.XmlSerializer headerSerializer = new System.Xml.Serialization.XmlSerializer(typeof(List<HeaderInfo>));
}