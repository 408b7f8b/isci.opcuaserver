using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using Opc.Ua;
using isci.Daten;

namespace isci.opcuaserver
{
    public class OpcUaServer : Opc.Ua.Server.StandardServer
    {
        [Newtonsoft.Json.JsonIgnore]
        private Datenstruktur Struktur;
        public string name = "opcUaServer";
        private ApplicationConfiguration application;

        [Newtonsoft.Json.JsonIgnore]
        private List<string> hinzugefügt = new List<string>();
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, object> puffer = new Dictionary<string, object>();
        public bool puffer_mutex = false;
        
        public void WriteLine(string content)
        {
            Console.WriteLine($"OPC-UA-Server {name}: {content}");
        }
        
        public static ApplicationConfiguration standardApplicationConfiguration(string name)
        {
            var application = new ApplicationConfiguration()
            {
                ApplicationName = name,
                ApplicationUri = "urn:localhost:UA:" + name,
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration()
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = @"Directory",
                        StorePath = @"CertificateStores/MachineDefault",
                        SubjectName = Utils.Format(@"CN={0}, DC={1}", name, System.Net.Dns.GetHostName())
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = @"Directory", 
                        StorePath = @"CertificateStores/UA Applications"
                    },
                    NonceLength = 32, 
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas()
                {
                    OperationTimeout = 600000,
                    MaxStringLength = 1048576,
                    MaxByteStringLength = 1048576,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 4194304,
                    MaxBufferSize = 65535,
                    ChannelLifetime = 300000,
                    SecurityTokenLifetime = 3600000
                },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection()
                    {
                        "opc.tcp://localhost:62541/" + name
                    },
                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None"
                        }
                    },
                    UserTokenPolicies = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
                    },
                    AlternateBaseAddresses = new StringCollection
                    {
                        "ocp.tcp://localhost:4840/" + name
                    },
                    DiagnosticsEnabled = true,
                    MaxSessionCount = 100,
                    MinSessionTimeout = 5000,
                    MaxSessionTimeout = 10000,
                    MaxBrowseContinuationPoints = 10,
                    MaxQueryContinuationPoints = 10,
                    MaxHistoryContinuationPoints = 100,
                    MaxRequestAge = 600000,
                    MinPublishingInterval = 100,
                    MaxPublishingInterval = 3600000,
                    PublishingResolution = 50,
                    MaxSubscriptionLifetime = 3600000,
                    MaxMessageQueueSize = 10,
                    MaxNotificationQueueSize = 100,
                    MaxNotificationsPerPublish = 1000,
                    MinMetadataSamplingInterval = 1000,
                    RegistrationEndpoint = new EndpointDescription()
                    {
                        EndpointUrl = "opc.tcp://localhost:4840",
                        Server = new ApplicationDescription()
                        {
                            ApplicationUri = "opc.tcp://localhost:4840",
                            ApplicationType = ApplicationType.DiscoveryServer,
                            DiscoveryUrls = new StringCollection(){
                                "opc.tcp://localhost:4840"
                            }
                        },
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = "",
                        UserIdentityTokens = new UserTokenPolicyCollection()
                        {

                        }
                    }
                }
            };

            return application;
        }

        public OpcUaServer(ApplicationConfiguration application = null, string name = "opcUaServer")
        {
            this.name = name;
            this.application = (application != null ? application : standardApplicationConfiguration(name));

            var instanz = new Opc.Ua.Configuration.ApplicationInstance(this.application);

            var tsk = instanz.CheckApplicationInstanceCertificate(false, 1024);
            try {
                tsk.Wait();
            } catch {
                var tsk2 = instanz.DeleteApplicationInstanceCertificate();
                tsk2.Wait();
                tsk = instanz.CheckApplicationInstanceCertificate(false, 1024);
                tsk.Wait();
            }
            

            //var createTask = Opc.Ua.Configuration.CreateApplicationInstanceCertificate(this.application, 128, 12);



            var resval = this.application.Validate(ApplicationType.Server);

            resval.Wait();

            
            var d = this.application.GetServerDomainNames();
        }

        public void Start()
        {
            WriteLine("Start");
            this.Start(application);
        }

        public void Stopp()
        {
            WriteLine("Stopp");
            this.Stop();
        }

        public void StrukturEintragen(Datenstruktur struktur)
        {
            WriteLine("StrukturEintragen");
            Struktur = struktur;
        }

        public void nodeHinzufügen(AddNodesItemCollection coll, string key)
        {
            if (hinzugefügt.Contains(key)) return;

            var node = new AddNodesItem();
            node.NodeAttributes = new ExtensionObject(
                new Opc.Ua.VariableAttributes()
                {
                    AccessLevel = AccessLevels.CurrentReadOrWrite, //item.Access == Accesslevel.READWRITE ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite, //item.Access == Accesslevel.READWRITE ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    SpecifiedAttributes = (uint) NodeAttributesMask.All,
                    WriteMask = (uint)AttributeWriteMask.None,
                    UserWriteMask = (uint)AttributeWriteMask.None,
                    MinimumSamplingInterval = 0,
                    Historizing = false
                }
            );

            string top = "";

            if (Struktur.dateneinträge.ContainsKey(key))
            {
                var item = Struktur.dateneinträge[key];

                node.NodeClass = NodeClass.Variable;
                /*switch (item.type)
                {
                    case Datentypen.UInt8: node.TypeDefinition = DataTypeIds.Byte; break;
                    case Datentypen.UInt16: node.TypeDefinition = DataTypeIds.UInt16; break;
                    case Datentypen.UInt32: node.TypeDefinition = DataTypeIds.UInt32; break;
                    case Datentypen.Int8: node.TypeDefinition = DataTypeIds.SByte; break;
                    case Datentypen.Int16: node.TypeDefinition = DataTypeIds.Int16; break;
                    case Datentypen.Int32: node.TypeDefinition = DataTypeIds.Int32; break;
                    case Datentypen.Bool: node.TypeDefinition = DataTypeIds.Boolean; break;
                    case Datentypen.Float: node.TypeDefinition = DataTypeIds.Float; break;
                    case Datentypen.Double: node.TypeDefinition = DataTypeIds.Double; break;
                    case Datentypen.String: node.TypeDefinition = DataTypeIds.String; break;
                }*/

                node.BrowseName = item.Identifikation.Split('.').Last();
                node.RequestedNewNodeId = new ExpandedNodeId(item.Identifikation, 3);

                top = "ns=3;s=" + item.Identifikation.Substring(0, item.Identifikation.LastIndexOf('.'));
            } else {
                node.NodeClass = NodeClass.Object;
                node.TypeDefinition = DataTypeIds.ObjectNode;

                node.BrowseName = key.Contains('.') ? key.Split('.').Last() : key;
                node.RequestedNewNodeId = new ExpandedNodeId(key, 3);

                if (key.Contains('.'))
                {
                    top = "ns=3;s=" + key.Substring(0, key.LastIndexOf('.'));
                } else {
                    top = "ns=0;i=85";
                }
            }
            
            if (!hinzugefügt.Contains(top) && top != "ns=0;i=85")
            {
                nodeHinzufügen(coll, top);
                hinzugefügt.Add(top);
            }
            node.ParentNodeId = new ExpandedNodeId(top); 

            WriteLine($"Trage {node.BrowseName} als {node.RequestedNewNodeId.ToString()} ein ");
            coll.Add(node);
        }

        public void DatenstrukturAufbauen()
        {
            WriteLine("DatenstrukturAufbauen");
            var r = new AddNodesItemCollection();
            //var w = new WriteValueCollection();

            Dictionary<string, ExpandedNodeId> Ordner_Nodes = new Dictionary<string, ExpandedNodeId>();

            var Ausstehend = Struktur.dateneinträge.Keys.ToList();
            Ausstehend.Sort();
            var entfernen = new List<string>();

            foreach (var existiert in hinzugefügt)
            {
                if (Ausstehend.Contains(existiert))
                {
                    Ausstehend.Remove(existiert);
                } else {
                    entfernen.Add(existiert);
                }
            }

            if (entfernen.Count > 0)
            {
                var req_del = new RequestHeader();
                var coll_del = new DeleteNodesItemCollection();
                var res_del = new StatusCodeCollection();
                var diag_del = new DiagnosticInfoCollection();

                foreach (var loeschen in entfernen)
                {
                    var item = new DeleteNodesItem();
                    item.NodeId = new NodeId(loeschen);
                    coll_del.Add(item);

                    hinzugefügt.Remove(loeschen);
                }

                this.DeleteNodes(req_del, coll_del, out res_del, out diag_del);
            }

            while (Ausstehend.Count > 0)
            {
                for (int i = Ausstehend.Count-1; i >= 0; --i)
                {
                    var item = Struktur.dateneinträge[Ausstehend[i]];
                    this.nodeHinzufügen(r, Ausstehend[i]);

                    hinzugefügt.Add(Ausstehend[i]);
                    Ausstehend.RemoveAt(i);                    
                }
            }

            var res = new AddNodesResultCollection();
            var diag = new DiagnosticInfoCollection();
            var req = new RequestHeader();
            this.AddNodes(req, r, out res, out diag);
        }

        public List<Datenmodell> DatenmodelleLiefern()
        {
            return new List<Datenmodell>();
        }

        public void DatenwertVerbreiten(string feld)
        {
            var datenfeld = Struktur.dateneinträge[feld];
            WriteInternal(datenfeld.Identifikation, datenfeld.Wert);
        }

        public void WriteInternal(string ident, object value)
        {
            var nodeId = NodeId.Parse(ident);
            var node = ServerInternal.CoreNodeManager.GetLocalNode(nodeId);
            if (node == null) return;
            node.Write(Attributes.Value, new DataValue(new Variant(value)));
        }

        public override ResponseHeader Write(RequestHeader requestHeader, WriteValueCollection nodesToWrite, out StatusCodeCollection results, out DiagnosticInfoCollection diagnosticInfos)
        {
            results = new StatusCodeCollection();
            diagnosticInfos = null;

            ValidateRequest(requestHeader);

            foreach (var item in nodesToWrite)
            {
                try
                {
                    var node = ServerInternal.CoreNodeManager.GetLocalNode(item.NodeId);
                    node.Write(Attributes.Value, item.Value);

                    var id = item.NodeId.ToString();

                    while(puffer_mutex)
                    {

                    }

                    puffer_mutex = true;

                    if (!puffer.ContainsKey(id))
                    {
                        puffer.Add(id, new object());
                    }

                    var dateneintrag = Struktur.dateneinträge[id];
                    switch (dateneintrag.type)
                    {
                        case Datentypen.UInt8: puffer[id] = item.Value.GetValue(typeof(System.Byte)); break;
                        case Datentypen.UInt16: puffer[id] = item.Value.GetValue(typeof(System.UInt16)); break;
                        case Datentypen.UInt32: puffer[id] = item.Value.GetValue(typeof(System.UInt32)); break;
                        case Datentypen.Int8: puffer[id] = item.Value.GetValue(typeof(System.SByte)); break;
                        case Datentypen.Int16: puffer[id] = item.Value.GetValue(typeof(System.Int16)); break;
                        case Datentypen.Int32: puffer[id]= item.Value.GetValue(typeof(System.Int32)); break;
                        case Datentypen.Bool: puffer[id] = item.Value.GetValue(typeof(System.Boolean)); break;
                        case Datentypen.Float: puffer[id] = item.Value.GetValue(typeof(float)); break;
                        case Datentypen.Double: puffer[id] = item.Value.GetValue(typeof(System.Double)); break;
                        case Datentypen.String: puffer[id] = item.Value.GetValue(typeof(System.String)); break;
                    }

                    puffer_mutex = false;

                    results.Add(StatusCodes.Good);
                } catch {
                    results.Add(StatusCodes.Bad);
                }
            }

            //return base.Write(requestHeader, nodesToWrite, out results, out diagnosticInfos);
            return CreateResponse(requestHeader, StatusCodes.Good);
        }

        public override ResponseHeader AddNodes(
        RequestHeader requestHeader,
        AddNodesItemCollection nodesToAdd,
        out AddNodesResultCollection results,
        out DiagnosticInfoCollection diagnosticInfos)
        {
            results = null;
            diagnosticInfos = null;

            ValidateRequest(requestHeader);

            foreach (var item in nodesToAdd)
            {
                WriteLine($"Füge Node {item.RequestedNewNodeId.ToString()} ein ");
                //NodeClass.Object
                if (item.NodeClass == NodeClass.Variable)
                {
                    var ident = item.RequestedNewNodeId.ToString();

                    var struktur_item = Struktur.dateneinträge[ident];
                    
                    var node = new VariableNode
                    {
                        NodeId = new NodeId(item.RequestedNewNodeId.Identifier, item.RequestedNewNodeId.NamespaceIndex),
                        DisplayName = new LocalizedText(item.BrowseName.Name),
                        References = new ReferenceNodeCollection(){ },
                        BrowseName = item.BrowseName,
                        Description = new LocalizedText(""),
                        NodeClass = NodeClass.Variable,
                        AccessLevel = ((Opc.Ua.VariableAttributes)item.NodeAttributes.Body).AccessLevel,
                        UserAccessLevel = ((Opc.Ua.VariableAttributes)item.NodeAttributes.Body).UserAccessLevel,
                        //Value = new Variant(((HESTA.Architektur.Strukturen.Datenstruktur)requestHeader.AdditionalHeader.Body)[item.RequestedNewNodeId.Identifier.ToString()].Wert)
                        Value = new Variant(Struktur.dateneinträge[ident].Wert)
                    };

                    switch (struktur_item.type)
                    {
                        case Datentypen.UInt8: node.DataType = DataTypeIds.Byte; break;
                        case Datentypen.UInt16: node.DataType = DataTypeIds.UInt16; break;
                        case Datentypen.UInt32: node.DataType = DataTypeIds.UInt32; break;
                        case Datentypen.Int8: node.DataType = DataTypeIds.SByte; break;
                        case Datentypen.Int16: node.DataType = DataTypeIds.Int16; break;
                        case Datentypen.Int32: node.DataType = DataTypeIds.Int32; break;
                        case Datentypen.Bool: node.DataType = DataTypeIds.Boolean; break;
                        case Datentypen.Float: node.DataType = DataTypeIds.Float; break;
                        case Datentypen.Double: node.DataType = DataTypeIds.Double; break;
                        case Datentypen.String: node.DataType = DataTypeIds.String; break;
                    }

                    //m_serverInternal.CoreNodeManager.AttachNode(node);
                    ServerInternal.CoreNodeManager.AttachNode(node);
                    //ServerInternal.CoreNodeManager.CreateReference(new NodeId(item.ParentNodeId.Identifier, item.ParentNodeId.NamespaceIndex), ReferenceTypeIds.Organizes, false, node.NodeId, false);
                    ServerInternal.CoreNodeManager.AddReference(new NodeId(item.ParentNodeId.Identifier, item.ParentNodeId.NamespaceIndex), ReferenceTypeIds.Organizes, false, node.NodeId, false);
                }
                else if (item.NodeClass == NodeClass.VariableType)
                {
                    var node = new VariableTypeNode
                    {
                        // TODO: Initialization
                    };
                    //m_serverInternal.CoreNodeManager.AttachNode(node);
                    ServerInternal.CoreNodeManager.AttachNode(node);
                }
                else if (item.NodeClass == NodeClass.Object)
                {
                    var ident = item.RequestedNewNodeId.Identifier.ToString();
                    var node = new ObjectNode
                    {
                        NodeId = new NodeId(item.RequestedNewNodeId.Identifier, item.RequestedNewNodeId.NamespaceIndex),
                        DisplayName = new LocalizedText(item.BrowseName.Name),
                        References = new ReferenceNodeCollection(){ },
                        BrowseName = item.BrowseName,
                        Description = new LocalizedText(""),
                        NodeClass = NodeClass.Object
                        //Value = new Variant(((HESTA.Architektur.Strukturen.Datenstruktur)requestHeader.AdditionalHeader.Body)[item.RequestedNewNodeId.Identifier.ToString()].Wert)
                    };

                    //m_serverInternal.CoreNodeManager.AttachNode(node);
                    ServerInternal.CoreNodeManager.AttachNode(node);
                    //ServerInternal.CoreNodeManager.CreateReference(new NodeId(item.ParentNodeId.Identifier, item.ParentNodeId.NamespaceIndex), ReferenceTypeIds.Organizes, false, node.NodeId, false);
                    ServerInternal.CoreNodeManager.AddReference(new NodeId(item.ParentNodeId.Identifier, item.ParentNodeId.NamespaceIndex), ReferenceTypeIds.Organizes, false, node.NodeId, false);
                }
                else
                {
                    // TODO
                }
            }

            return CreateResponse(requestHeader, StatusCodes.Good);
        }
    }
}