using System;
using System.Net;


internal struct MsmqAddress
{

    public readonly string Queue;

    public readonly string Machine;

    public static MsmqAddress Parse(string address)
    {
        var split = address.Split('@');

        if (split.Length > 2)
        {
            var message = string.Format("Address contains multiple @ characters. Address supplied: '{0}'", address);
            throw new ArgumentException(message, "address");
        }

        var queue = split[0];
        if (string.IsNullOrWhiteSpace(queue))
        {
            var message = string.Format("Empty queue part of address. Address supplied: '{0}'", address);
            throw new ArgumentException(message, "address");
        }

        string machineName;
        if (split.Length == 2)
        {
            machineName = split[1];
            if (string.IsNullOrWhiteSpace(machineName))
            {
                var message = string.Format("Empty machine part of address. Address supplied: '{0}'", address);
                throw new ArgumentException(message, "address");
            }
            machineName = ApplyLocalMachineConventions(machineName);
        }
        else
        {
            machineName = Environment.MachineName;
        }

        return new MsmqAddress(queue, machineName);
    }

    static string ApplyLocalMachineConventions(string machineName)
    {
        if (
            machineName == "." ||
            machineName.ToLower() == "localhost" ||
            machineName == IPAddress.Loopback.ToString()
            )
        {
            return Environment.MachineName;
        }
        return machineName;
    }

    /// <summary>
    /// Instantiate a new Address for a known queue on a given machine.
    /// </summary>
    ///<param name="queueName">The queue name.</param>
    ///<param name="machineName">The machine name.</param>
    public MsmqAddress(string queueName, string machineName)
    {
        Queue = queueName;
        Machine = machineName;
    }

    /// <summary>
    /// Returns a string representation of the address.
    /// </summary>
    public override string ToString()
    {
        return Queue + "@" + Machine;
    }

    /// <summary>
    /// Returns a string representation of the address.
    /// </summary>
    public string ToString(string qualifier)
    {
        return Queue + "." + qualifier + "@" + Machine;
    }

}