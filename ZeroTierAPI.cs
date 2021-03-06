﻿using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using LibZeroTier;
using System.IO;
using System.Threading;

namespace ZeroTier
{
    public class Networking
    {
        public static async Task<string> CreateNetworkAsync()
        {
            return await WebApi.CreateNewNetwork();
        }
        public static async Task DeleteNetworkAsync(string NetworkId)
        {
            await WebApi.DeleteNetwork(NetworkId);
        }
    }
    public class ZeroTeirAPI
    {
        public void CheckForPropertyUpdate()
        {
            List<ZeroTierNetwork> Networks = new List<ZeroTierNetwork>();
            while (this.ZeroTeirHandler == null) { }
            Networks.AddRange(this.ZeroTeirHandler.GetNetworks());
            while (this.ZeroTeirHandler != null)
            {
                if (Process.GetProcessesByName("ZeroTier One").Length <= 0)
                {
                    this.ZeroTeirHandler = null;
                    break;
                }
                List<ZeroTierNetwork> API_Network_List = new List<ZeroTierNetwork>();
                API_Network_List.AddRange(this.ZeroTeirHandler.GetNetworks());
                List<ZeroTierNetwork> NetworksCopy = new List<ZeroTierNetwork>();
                NetworksCopy.AddRange(Networks);
                foreach (ZeroTierNetwork OldNet in NetworksCopy)
                {
                    ZeroTierNetwork GetNetId = GetNetworkById(OldNet.NetworkId, API_Network_List);
                    if (GetNetId == null)
                    {
                        Networks.Remove(OldNet);
                        OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = StatusChanged.NetworkListChanged, Property = "Network Removed", Value = OldNet }, null);
                        if (OldNet.IsConnected)
                            OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = StatusChanged.LeftNetwork, Property = "Left Network", Value = OldNet.NetworkId }, null);
                    }
                }
                foreach (ZeroTierNetwork NewNet in API_Network_List)
                {
                    ZeroTierNetwork GetNetId = GetNetworkById(NewNet.NetworkId, Networks);
                    if (GetNetId == null)
                    {
                        Networks.Add(NewNet);
                        OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = StatusChanged.NetworkListChanged, Property = "Network Added", Value = NewNet }, null);
                        if (NewNet.IsConnected)
                        {
                            OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = StatusChanged.JoinedNetwork, Property = "Joined Network", Value = NewNet.NetworkId }, null);
                            OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = StatusChanged.OnlineStatusChanged, Property = "status", Value = NewNet.NetworkStatus }, null);
                        }
                    }
                    else
                    {
                        bool ValWasChanged = false;
                        var NewNetJson = JObject.Parse(JsonConvert.SerializeObject(NewNet));
                        foreach (var Property in JObject.Parse(JsonConvert.SerializeObject(GetNetId)))
                        {
                            string NewVal = NewNetJson.GetValue(Property.Key).ToString();
                            if (Property.Value.ToString() != NewVal)
                            {
                                ValWasChanged = true;
                                OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = GetChangedStatus(Property.Key), Property = Property.Key, Value = NewVal }, null);
                            }
                        }
                        if (ValWasChanged)
                            Networks[Networks.IndexOf(GetNetId)] = NewNet;
                    }
                }
            }
            OnNetworkStatusChanged(new NetworkChangedEventArgs() { Change = StatusChanged.UnexpectedShutdown, Property = "Network", Value = "Connection Lost" }, null);
        }
        public ZeroTierNetwork GetNetworkById(string id, List<ZeroTierNetwork> NetworkList)
        {
            foreach (var network in NetworkList)
            {
                if (network.NetworkId == id)
                {
                    return network;
                }
            }
            return null;
        }
        public StatusChanged GetChangedStatus(string Object)
        {
            if (Object == "status")
                Debug.WriteLine("Status Change!");
            switch (Object)
            {
                case "name":
                    return StatusChanged.NetworkPropertiesChanged;
                case "status":
                    return StatusChanged.OnlineStatusChanged;
                case "type":
                    return StatusChanged.NetworkPropertiesChanged;
                case "broadcastEnabled":
                    return StatusChanged.NetworkPropertiesChanged;
                case "routes":
                    return StatusChanged.NetworkPropertiesChanged;
                case "allowManaged":
                    return StatusChanged.NetworkPropertiesChanged;
                default:
                    return StatusChanged.GenericPropertyChange;
            }

        }
        public class NetworkChangedEventArgs
        {
            public StatusChanged Change { get; set; }
            public string Property { get; set; }
            public object Value { get; set; }
        }
        public event EventHandler NetworkStatusChanged;
        protected virtual void OnNetworkStatusChanged(NetworkChangedEventArgs args, EventArgs e)
        {
            EventHandler handler = NetworkStatusChanged;
            handler?.Invoke(args, e);
        }
        public enum StatusChanged
        {
            CreatedNetwork,
            DestroyedNetwork,
            JoinedNetwork,
            LeftNetwork,
            ConnectionTimeout,
            UnexpectedShutdown,
            OnlineStatusChanged,
            NodeAdressedChanged,
            NetworkListChanged,
            NetworkPropertiesChanged,
            UserStatusChanged,
            GenericPropertyChange
        }
        public string NetworkId;
        public APIHandler ZeroTeirHandler;
        Thread APIupdater;
        public void NewApiCreated()
        {
            APIupdater = new Thread(CheckForPropertyUpdate);
            APIupdater.Start();
        }
        public string GetNetStatus()
        {
            try
            {
                return this.ZeroTeirHandler.GetNetworks()[0].NetworkStatus;
            }
            catch
            {
                return "[ZeroTier] [API]: API Handler Not Running!";
            }
        }
        public string GetZeroStatus()
        {
            try
            {
                return JsonConvert.SerializeObject(this.ZeroTeirHandler.GetStatus());
            }
            catch
            {
                return "[ZeroTier] [API]: API Handler Not Running!";
            }
        }
        public void StartServer(string Network = null)
        {
            StartServerAsync(Network);
        }
        public async Task StartServerAsync(string Network = null)
        {
            Console.WriteLine("[ZeroTier] [API]: Locating ...");
            // start zero teir if it has not been done already
            Process[] Zero = Process.GetProcessesByName("ZeroTier One");
            if (Zero.Length == 0)
            {
                Process proccess = new Process();
                proccess.StartInfo.FileName = @"C:\Program Files (x86)\ZeroTier\One\ZeroTier One.exe";
                proccess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proccess.StartInfo.CreateNoWindow = true;
                proccess.Start();
            }
            Console.WriteLine("[ZeroTier] [API]: Initialized!");
            Console.WriteLine("[ZeroTier] [API]: Loading API...");
            if (ZeroTeirHandler == null)
            {
                ZeroTeirHandler = new APIHandler();
                NewApiCreated();
                Console.WriteLine("[ZeroTier] [API]: API Created!");
            }
            else
            {
                Console.WriteLine("[ZeroTier] [API]: API Loaded!");
            }

            if (Network == null)
                Network = await Networking.CreateNetworkAsync();
            Console.WriteLine("[ZeroTier] [API-NET]: P2P Network Created!");
            // leave any joined networks
            foreach (var LocalNet in ZeroTeirHandler.GetNetworks())
                ZeroTeirHandler.LeaveNetwork(LocalNet.NetworkId);
            // make sure the network is added, and joined properly
            bool Connected = false;
            while (!Connected)
            {
                ZeroTeirHandler.JoinNetwork(Network);
                foreach (var LocalNet in ZeroTeirHandler.GetNetworks())
                    if (LocalNet.IsConnected && LocalNet.NetworkId == Network)
                        Connected = true;
            }
            NetworkId = Network;
            Console.WriteLine("[ZeroTier] [Network]: P2P Connection Established!");
        }
        public void JoinServer(string NetworkId)
        {
            StartServer(NetworkId);
        }
        public void StopServer()
        {
            // get zero teir process(es) and kill em
            Process[] Zero = Process.GetProcessesByName("ZeroTier One");
            foreach (var item in Zero) { item.Kill(); item.WaitForExit(); }
            Console.WriteLine("[ZeroTier] [API]: Stopped all ZeroTier processes");
            // delete network on website
            Networking.DeleteNetworkAsync(this.NetworkId);
            Console.WriteLine("[ZeroTier] [API-NET]: Deleted P2P Network");
            // Delete all network history
            DeleteAllNonConnectedNetworks();
            Console.WriteLine("[ZeroTier] [Network]: Deleted Network History");
            Console.WriteLine("[ZeroTier] [Network]: P2P Connection Removed");
            this.ZeroTeirHandler = null;
        }
        public static HttpClient client = new HttpClient();
        public async Task DeleteAllNetworks()
        {
            // setup headers
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", "XlOrMp71uEQdxFT1D0bzjkkBjJCOCbvA");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // get network list
            var Get = client.GetAsync("https://my.zerotier.com/api/network");
            string content = await Get.Result.Content.ReadAsStringAsync();
            var temp = JObject.Parse("{ \"Array\":" + content + "}");
            // delete all networks
            foreach (var item in temp["Array"])
            {
                var netid = item["id"].ToString();
                WebApi.DeleteNetwork(netid);
            }
        }
        public static void DeleteAllNonConnectedNetworks()
        {
            string AppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string NetworksFile = Path.Combine(AppData, "ZeroTier", "One", "Networks.dat");
            File.Delete(NetworksFile);
        }
    }
    public class WebApi
    {
        public static HttpClient client = new HttpClient();

        public static async Task<string> CreateNewNetwork(string NetworkName = "Multiplayer Server", string NetworkDescription = "Multiplayer Server")
        {
            string NetworkId = "";
            try
            {
                // setup headers
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", "XlOrMp71uEQdxFT1D0bzjkkBjJCOCbvA");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // get network list
                var Get = client.GetAsync("https://my.zerotier.com/api/network");
                string content = await Get.Result.Content.ReadAsStringAsync();
                var temp = JObject.Parse("{ \"Array\":" + content + "}");
                List<string> NetworkIds = new List<string>();
                // append network names to list
                foreach (var item in temp["Array"])
                {
                    var netid = item["id"].ToString();
                    NetworkIds.Add(netid);
                }
                // generate new network id until the network id is unique
                while (NetworkIds.Contains(NetworkId) || NetworkId.Length < 16)
                {
                    NetworkId = Math.Floor((new Random().NextDouble() + 0.01) * Math.Pow(10, 16)).ToString();
                }
                // setup new network json post
                long CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Network net = new Network()
                {
                    Id = NetworkId,
                    Type = "Network",
                    Clock = 1608235389104,
                    Config = new Config()
                    {
                        AuthTokens = null,
                        CreationTime = CurrentTime,
                        Capabilities = new List<object>(),
                        EnableBroadcast = true,
                        Id = NetworkId,
                        IpAssignmentPools = new List<IpAssignmentPool>()
                        {
                            new IpAssignmentPool()
                            {
                                IpRangeStart = "10.10.10.0",
                                IpRangeEnd = "10.10.10.254"
                            }
                        },
                        LastModified = CurrentTime,
                        Mtu = 2800,
                        MulticastLimit = 32,
                        Name = "Multiplayer Server",
                        Private = false,
                        RemoteTraceLevel = 0,
                        RemoteTraceTarget = null,
                        Routes = new List<Route>()
                        {
                            new Route()
                            {
                                Target = "10.10.10.0/24",
                                Via = "10.10.10.1"
                            }
                        },
                        Rules = new List<Rule>()
                        {
                            new Rule()
                            {
                                EtherType = 2048,
                                Not = true,
                                Or = false,
                                Type = "MATCH_ETHERTYPE"
                            },
                            new Rule()
                            {
                                EtherType = 2054,
                                Not = true,
                                Or = false,
                                Type = "MATCH_ETHERTYPE"
                            },
                            new Rule()
                            {
                                EtherType = 34525,
                                Not = true,
                                Or = false,
                                Type = "MATCH_ETHERTYPE"
                            },
                            new Rule()
                            {
                                Type = "ACTION_DROP"
                            },
                            new Rule()
                            {
                                Type = "ACTION_ACCEPT"
                            }
                        },
                        Tags = new List<object>(),
                        V4AssignMode = new V4AssignMode()
                        {
                            Zt = true
                        },
                        V6AssignMode = new V6AssignMode()
                        {
                            The6Plane = false,
                            Rfc4193 = false,
                            Zt = false
                        },
                        Dns = new Dns()
                        {
                            Domain = "",
                            Servers = null
                        }
                    },
                    Description = "Multiplayer Server",
                    RulesSource = "",
                    Permissions = null,
                    OwnerId = "56e2eba4-db42-4082-9456-fe22051d54b8",
                    OnlineMemberCount = 0,
                    AuthorizedMemberCount = null,
                    TotalMemberCount = 0,
                    CapabilitiesByName = new SByName(),
                    TagsByName = new SByName(),
                    Ui = null
                };
                // post new network
                var res = client.PostAsync("https://my.zerotier.com/api/network", new StringContent(Serialize.ToJson(net)));
                // deal with network errors or completion 
                try
                {
                    res.Result.EnsureSuccessStatusCode();
                    //Debug.WriteLine("Response " + res.Result.Content.ReadAsStringAsync().Result + Environment.NewLine);
                    var netId = JObject.Parse(res.Result.Content.ReadAsStringAsync().Result);
                    NetworkId = netId["id"].ToString();
                    // Debug.WriteLine(NetworkId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error " + res + "\r\nError " + ex.ToString());
                }
                // log network response
                //Debug.WriteLine("Response: {0}", res);
            }
            catch (Exception ex)
            {
                // log network, or json/data handling errors
                Debug.WriteLine(ex.ToString());
            }
            return NetworkId;
        }
        public static async Task DeleteNetwork(string NetworkId)
        {
            // setup headers
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", "XlOrMp71uEQdxFT1D0bzjkkBjJCOCbvA");
            // delete network
            var Get = client.DeleteAsync("https://my.zerotier.com/api/network/" + NetworkId);
        }
    }
    public partial class Network
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty("clock", NullValueHandling = NullValueHandling.Ignore)]
        public long? Clock { get; set; }

        [JsonProperty("config", NullValueHandling = NullValueHandling.Ignore)]
        public Config Config { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("rulesSource", NullValueHandling = NullValueHandling.Ignore)]
        public string RulesSource { get; set; }

        [JsonProperty("permissions", NullValueHandling = NullValueHandling.Ignore)]
        public string Permissions { get; set; }

        [JsonProperty("ownerId", NullValueHandling = NullValueHandling.Ignore)]
        public string? OwnerId { get; set; }

        [JsonProperty("onlineMemberCount", NullValueHandling = NullValueHandling.Ignore)]
        public long? OnlineMemberCount { get; set; }

        [JsonProperty("authorizedMemberCount", NullValueHandling = NullValueHandling.Ignore)]
        public long? AuthorizedMemberCount { get; set; }

        [JsonProperty("totalMemberCount", NullValueHandling = NullValueHandling.Ignore)]
        public long? TotalMemberCount { get; set; }

        [JsonProperty("capabilitiesByName", NullValueHandling = NullValueHandling.Ignore)]
        public SByName CapabilitiesByName { get; set; }

        [JsonProperty("tagsByName", NullValueHandling = NullValueHandling.Ignore)]
        public SByName TagsByName { get; set; }

        [JsonProperty("ui")]
        public object Ui { get; set; }
    }

    public partial class SByName
    {

    }

    public partial class Config
    {
        [JsonProperty("authTokens")]
        public object AuthTokens { get; set; }

        [JsonProperty("creationTime", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreationTime { get; set; }

        [JsonProperty("capabilities", NullValueHandling = NullValueHandling.Ignore)]
        public List<object> Capabilities { get; set; }

        [JsonProperty("enableBroadcast", NullValueHandling = NullValueHandling.Ignore)]
        public bool? EnableBroadcast { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("ipAssignmentPools", NullValueHandling = NullValueHandling.Ignore)]
        public List<IpAssignmentPool> IpAssignmentPools { get; set; }

        [JsonProperty("lastModified", NullValueHandling = NullValueHandling.Ignore)]
        public long? LastModified { get; set; }

        [JsonProperty("mtu", NullValueHandling = NullValueHandling.Ignore)]
        public long? Mtu { get; set; }

        [JsonProperty("multicastLimit", NullValueHandling = NullValueHandling.Ignore)]
        public long? MulticastLimit { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("private", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Private { get; set; }

        [JsonProperty("remoteTraceLevel", NullValueHandling = NullValueHandling.Ignore)]
        public long? RemoteTraceLevel { get; set; }

        [JsonProperty("remoteTraceTarget")]
        public object RemoteTraceTarget { get; set; }

        [JsonProperty("routes", NullValueHandling = NullValueHandling.Ignore)]
        public List<Route> Routes { get; set; }

        [JsonProperty("rules", NullValueHandling = NullValueHandling.Ignore)]
        public List<Rule> Rules { get; set; }

        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public List<object> Tags { get; set; }

        [JsonProperty("v4AssignMode", NullValueHandling = NullValueHandling.Ignore)]
        public V4AssignMode V4AssignMode { get; set; }

        [JsonProperty("v6AssignMode", NullValueHandling = NullValueHandling.Ignore)]
        public V6AssignMode V6AssignMode { get; set; }

        [JsonProperty("dns", NullValueHandling = NullValueHandling.Ignore)]
        public Dns Dns { get; set; }
    }

    public partial class Dns
    {
        [JsonProperty("domain", NullValueHandling = NullValueHandling.Ignore)]
        public string Domain { get; set; }

        [JsonProperty("servers")]
        public object Servers { get; set; }
    }

    public partial class IpAssignmentPool
    {
        [JsonProperty("ipRangeStart", NullValueHandling = NullValueHandling.Ignore)]
        public string IpRangeStart { get; set; }

        [JsonProperty("ipRangeEnd", NullValueHandling = NullValueHandling.Ignore)]
        public string IpRangeEnd { get; set; }
    }

    public partial class Route
    {
        [JsonProperty("target", NullValueHandling = NullValueHandling.Ignore)]
        public string Target { get; set; }
        [JsonProperty("via", NullValueHandling = NullValueHandling.Ignore)]
        public string Via { get; set; }
    }

    public partial class Rule
    {
        [JsonProperty("etherType", NullValueHandling = NullValueHandling.Ignore)]
        public long? EtherType { get; set; }

        [JsonProperty("not", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Not { get; set; }

        [JsonProperty("or", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Or { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }
    }

    public partial class V4AssignMode
    {
        [JsonProperty("zt", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Zt { get; set; }
    }

    public partial class V6AssignMode
    {
        [JsonProperty("6plane", NullValueHandling = NullValueHandling.Ignore)]
        public bool? The6Plane { get; set; }

        [JsonProperty("rfc4193", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Rfc4193 { get; set; }

        [JsonProperty("zt", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Zt { get; set; }
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
                {
                    new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
                },
        };
    }
    public partial class Network
    {
        public static Network FromJson(string json) => JsonConvert.DeserializeObject<Network>(json, ZeroTier.Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this ZeroTier.Network self) => JsonConvert.SerializeObject(self, ZeroTier.Converter.Settings);
    }
}
