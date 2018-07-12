using System;
using System.Text;
using EventStore.ClientAPI;

namespace SoftwarePioniere.EventStore
{
    public class EventStoreOptions
    {
        public EventStoreOptions()
        {
            ClusterIpEndpoints = new string[0];
            ClusterHttpPorts = new int[0];
        }

        /// <summary>
        /// Customizen der Connection Settings
        /// </summary>
        public Action<ConnectionSettingsBuilder> ConnectionSetup { get; set; }

        /// <summary>
        ///     Verbindung zum EventStore - IP Adresse
        /// </summary>
        public string IpEndPoint { get; set; } = "127.0.0.1";

        /// <summary>
        ///     Verbindung zum EventStore - IP Adresse
        /// </summary>
        public string AdminPassword { get; set; } = "changeit";

        /// <summary>
        ///     Verbindung zum EventStore - IP Adresse
        /// </summary>
        public string OpsPassword { get; set; } = "changeit";


        /// <summary>
        ///     Verbindung zum EventStore - Port
        /// </summary>
        public int TcpPort { get; set; } = 1113;

        /// <summary>
        /// Port f�r HTTP Verbidnung
        /// </summary>
        public int HttpPort { get; set; } = 2113;

        /// <summary>
        /// Port für die Sichere Tcp Verbindung
        /// </summary>
        public int ExtSecureTcpPort { get; set; } = 1115;

        /// <summary>
        /// Gibt an, ob ssl verwendet werden sollen
        /// </summary>
        public bool UseSslCertificate { get; set; }

        /// <summary>
        /// Target Host für SSL
        /// </summary>
        public string SslTargetHost { get; set; } = "softwarepioniere_dev";

        /// <summary>
        /// Server Validierung für SSL aktiv
        /// </summary>
        public bool SslValidateServer { get; set; }

        /// <summary>
        ///     Verbindung zum EventStore - IP Adresse
        /// </summary>
        public string AdminUsername { get; set; } = "admin";

        /// <summary>
        ///     Verbindung zum EventStore - IP Adresse
        /// </summary>
        public string OpsUsername { get; set; } = "ops";

        public bool UseCluster { get; set; }

        public string[] ClusterIpEndpoints { get; set; }

        public int[] ClusterHttpPorts { get; set; }

        public override string ToString()
        {
            if (UseCluster)
            {
                var sb = new StringBuilder();
                foreach (var s in ClusterIpEndpoints)
                {
                    if (sb.Length > 0)
                        sb.Append(",");

                    sb.Append($"{s}");
                }
                return $"Cluster IpEndPoints: {sb} // Username: {OpsUsername} // TcpPort: {TcpPort} // HttpPort: {HttpPort} // UseSsl: {UseSslCertificate} //  SslTcpPort: {ExtSecureTcpPort}";

            }
            return $"IpEndPoint: {IpEndPoint} // Username: {OpsUsername} // TcpPort: {TcpPort} // HttpPort: {HttpPort} // UseSsl: {UseSslCertificate} //  SslTcpPort: {ExtSecureTcpPort}";
        }
    }
}