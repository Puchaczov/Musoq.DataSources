﻿using System;
using System.Collections.Generic;
using k8s.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;
using Musoq.Tests.Common;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Kubernetes.Tests;

[TestClass]
public class KubernetesTests
{
    [TestMethod]
    public void WhenDeploymentsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();

        api.Setup(f => f.ListDeploymentsForAllNamespaces())
            .Returns(new V1DeploymentList
            {
                Items = new List<V1Deployment>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                            Generation = 1,
                            ResourceVersion = "ResourceVersion",
                        },
                        Spec = new V1DeploymentSpec
                        {
                            Template = new V1PodTemplateSpec
                            {
                                Spec = new V1PodSpec
                                {
                                    Containers = new List<V1Container>
                                    {
                                        new()
                                        {
                                            Image = "Image",
                                            ImagePullPolicy = "ImagePullPolicy",
                                            Name = "Name"
                                        }
                                    },
                                    RestartPolicy = "Always"
                                }
                            }
                        },
                        Status = new V1DeploymentStatus
                        {
                            Conditions = new List<V1DeploymentCondition>
                            {
                                new()
                                {
                                    Status = "True",
                                    Reason = "Reason",
                                    Message = "Message"
                                }
                            }
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, CreationTimestamp, Generation, ResourceVersion, Images, ImagePullPolicies, RestartPolicy, ContainersNames, Status from #kubernetes.deployments()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual(DateTime.MinValue, table[0][2]);
        Assert.AreEqual(1L, table[0][3]);
        Assert.AreEqual("ResourceVersion", table[0][4]);
        Assert.AreEqual("Image", table[0][5]);
        Assert.AreEqual("ImagePullPolicy", table[0][6]);
        Assert.AreEqual("Always", table[0][7]);
        Assert.AreEqual("Name", table[0][8]);
        Assert.AreEqual("True", table[0][9]);
        
        Assert.AreEqual(10, table[0].Count);
    }
    
    [TestMethod]
    public void WhenPodsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();

        api.Setup(f => f.ListPodsForAllNamespaces())
            .Returns(new V1PodList
            {
                Items = new List<V1Pod>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace"
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new()
                                {
                                    Image = "Image",
                                    ImagePullPolicy = "ImagePullPolicy",
                                    Name = "Name"
                                }
                            },
                            RestartPolicy = "Always"
                        },
                        Status = new V1PodStatus
                        {
                            Conditions = new List<V1PodCondition>
                            {
                                new()
                                {
                                    Status = "True",
                                    Reason = "Reason",
                                    Message = "Message"
                                }
                            },
                            ContainerStatuses = new List<V1ContainerStatus>
                            {
                                new()
                                {
                                    Ready = true,
                                    RestartCount = 0,
                                    State = new V1ContainerState
                                    {
                                        Running = new V1ContainerStateRunning()
                                    }
                                }
                            },
                            PodIP = "1.2.3.4",
                            Phase = "Phase"
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, ContainersNames, PF, Ready, Restarts, Statuses, IP from #kubernetes.pods()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("Name", table[0][2]);
        Assert.AreEqual("Phase", table[0][3]);
        Assert.AreEqual(true, table[0][4]);
        Assert.AreEqual("0", table[0][5]);
        Assert.AreEqual("Running", table[0][6]);
        Assert.AreEqual("1.2.3.4", table[0][7]);
        
        Assert.AreEqual(8, table[0].Count);
    }
    
    [TestMethod]
    public void WhenServicesQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();

        api.Setup(f => f.ListServicesForAllNamespaces())
            .Returns(new V1ServiceList
            {
                Items = new List<V1Service>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace"
                        },
                        Spec = new V1ServiceSpec
                        {
                            Type = "Type",
                            ClusterIP = "ClusterIP",
                            ExternalIPs = new List<string>
                            {
                                "ExternalIP"
                            },
                            Ports = new List<V1ServicePort>
                            {
                                new()
                                {
                                    Name = "Name",
                                    Port = 1,
                                    Protocol = "Protocol",
                                    TargetPort = 2
                                }
                            }
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Type, ClusterIP, ExternalIPs, Ports from #kubernetes.services()";
        
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("Type", table[0][2]);
        Assert.AreEqual("ClusterIP", table[0][3]);
        Assert.AreEqual("ExternalIP", table[0][4]);
        Assert.AreEqual("1", table[0][5]);
        
        Assert.AreEqual(6, table[0].Count);
    }

    [TestMethod]
    public void WhenNodesQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListNodes())
            .Returns(new V1NodeList
            {
                Items = new List<V1Node>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                        Spec = new V1NodeSpec
                        {
                            Taints = new List<V1Taint>
                            {
                                new()
                                {
                                    Effect = "Effect",
                                    Key = "Key",
                                    Value = "Value"
                                }
                            }
                        },
                        Status = new V1NodeStatus
                        {
                            Addresses = new List<V1NodeAddress>
                            {
                                new()
                                {
                                    Address = "Address",
                                    Type = "Type"
                                }
                            },
                            Allocatable = new Dictionary<string, ResourceQuantity>
                            {
                                {"cpu", new ResourceQuantity("1")},
                                {"memory", new ResourceQuantity("2")}
                            },
                            Conditions = new List<V1NodeCondition>
                            {
                                new()
                                {
                                    Status = "True",
                                    Type = "Type",
                                    Reason = "Reason",
                                    Message = "Message"
                                }
                            },
                            DaemonEndpoints = new V1NodeDaemonEndpoints
                            {
                                KubeletEndpoint = new V1DaemonEndpoint
                                {
                                    Port = 1
                                }
                            },
                            NodeInfo = new V1NodeSystemInfo
                            {
                                Architecture = "Architecture",
                                BootID = "BootID",
                                ContainerRuntimeVersion = "ContainerRuntimeVersion",
                                KernelVersion = "KernelVersion",
                                KubeProxyVersion = "KubeProxyVersion",
                                KubeletVersion = "KubeletVersion",
                                MachineID = "MachineID",
                                OperatingSystem = "OperatingSystem",
                                SystemUUID = "SystemUUID"
                            },
                            Phase = "Phase",
                        },
                    }
                }
            });
        
        var query = "select Name, Status, Roles, Age, Version, Kernel, OS, Architecture, ContainerRuntime, Cpu, Memory from #kubernetes.nodes()";
        
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("True", table[0][1]);
        Assert.AreEqual("Key", table[0][2]);
        Assert.AreEqual(DateTime.MinValue, table[0][3]);
        Assert.AreEqual("KubeletVersion", table[0][4]);
        Assert.AreEqual("KernelVersion", table[0][5]);
        Assert.AreEqual("OperatingSystem", table[0][6]);
        Assert.AreEqual("Architecture", table[0][7]);
        Assert.AreEqual("ContainerRuntimeVersion", table[0][8]);
        Assert.AreEqual("1", table[0][9]);
        Assert.AreEqual("2", table[0][10]);
        
        Assert.AreEqual(11, table[0].Count);
    }

    [TestMethod]
    public void WhenConfigmapsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListConfigMapsForAllNamespaces())
            .Returns(new V1ConfigMapList
            {
                Items = new List<V1ConfigMap>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                    }
                }
            });
        
        var query = "select Name, Namespace, Age from #kubernetes.configmaps()";
        
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual(DateTime.MinValue, table[0][2]);
        
        Assert.AreEqual(3, table[0].Count);
    }

    [TestMethod]
    public void WhenCronJobsRequested_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListCronJobsForAllNamespaces())
            .Returns(new V1CronJobList
            {
                Items = new List<V1CronJob>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                        Spec = new V1CronJobSpec
                        {
                            Schedule = "Schedule"
                        },
                        Status = new V1CronJobStatus
                        {
                            Active = new List<V1ObjectReference>
                            {
                                new()
                                {
                                    Name = "Status1",
                                    NamespaceProperty = "Namespace"
                                }
                            },
                            LastScheduleTime = DateTime.MinValue
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Schedule, Statuses, LastScheduleTime from #kubernetes.cronjobs()";
        
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("Schedule", table[0][2]);
        Assert.AreEqual("Status1", table[0][3]);
        Assert.AreEqual(DateTime.MinValue, table[0][4]);
        
        Assert.AreEqual(5, table[0].Count);
    }

    [TestMethod]
    public void WhenDaemonSetsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListDaemonSetsForAllNamespaces())
            .Returns(new V1DaemonSetList
            {
                Items = new List<V1DaemonSet>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                        Spec = new V1DaemonSetSpec
                        {
                            Selector = new V1LabelSelector
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    {"Key", "Value"}
                                }
                            },
                            Template = new V1PodTemplateSpec
                            {
                                Spec = new V1PodSpec
                                {
                                    Containers = new List<V1Container>
                                    {
                                        new()
                                        {
                                            Image = "Image",
                                            ImagePullPolicy = "ImagePullPolicy",
                                            Name = "Name"
                                        }
                                    },
                                    RestartPolicy = "Always"
                                }
                            }
                        },
                        Status = new V1DaemonSetStatus
                        {
                            CurrentNumberScheduled = 1,
                            DesiredNumberScheduled = 1,
                            NumberAvailable = 1,
                            NumberMisscheduled = 1,
                            NumberReady = 1,
                            NumberUnavailable = 1,
                            ObservedGeneration = 1,
                            UpdatedNumberScheduled = 1
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Desired, Current, Ready, UpToDate, Available, Age from #kubernetes.daemonsets()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual(1, table[0][2]);
        Assert.AreEqual(1, table[0][3]);
        Assert.AreEqual(1, table[0][4]);
        Assert.AreEqual(1, table[0][5]);
        Assert.AreEqual(1, table[0][6]);
        Assert.AreEqual(DateTime.MinValue, table[0][7]);
        
        Assert.AreEqual(8, table[0].Count);
    }

    [TestMethod]
    public void WhenIngressesQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListIngressesForAllNamespaces())
            .Returns(new V1IngressList
            {
                Items = new List<V1Ingress>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                        Spec = new V1IngressSpec
                        {
                            IngressClassName = "Class",
                            Rules = new List<V1IngressRule>
                            {
                                new()
                                {
                                    Host = "Host",
                                    Http = new V1HTTPIngressRuleValue
                                    {
                                        Paths = new List<V1HTTPIngressPath>
                                        {
                                            new()
                                            {
                                                Path = "Path"
                                            }
                                        }
                                    }
                                }
                            },
                            Tls = new List<V1IngressTLS>()
                            {
                                new()
                                {
                                    Hosts = new List<string>()
                                    {
                                        "Hosts"
                                    }
                                }
                            }
                        },
                        Status = new V1IngressStatus
                        {
                            LoadBalancer = new V1IngressLoadBalancerStatus
                            {
                                Ingress = new List<V1IngressLoadBalancerIngress>
                                {
                                    new()
                                    {
                                        Hostname = "Hostname",
                                        Ip = "Ip"
                                    }
                                }
                            }
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Class, Hosts, Address, Ports, Age from #kubernetes.ingresses()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("Class", table[0][2]);
        Assert.AreEqual("Host", table[0][3]);
        Assert.AreEqual("Hostname", table[0][4]);
        Assert.AreEqual("Hosts", table[0][5]);
        Assert.AreEqual(DateTime.MinValue, table[0][6]);
        
        Assert.AreEqual(7, table[0].Count);
    }
    
    [TestMethod]
    public void WhenJobsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListJobsForAllNamespaces())
            .Returns(new V1JobList
            {
                Items = new List<V1Job>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                        Spec = new V1JobSpec
                        {
                            Parallelism = 1,
                            Completions = 1,
                            Template = new V1PodTemplateSpec
                            {
                                Spec = new V1PodSpec
                                {
                                    Containers = new List<V1Container>
                                    {
                                        new()
                                        {
                                            Image = "Image",
                                            ImagePullPolicy = "ImagePullPolicy",
                                            Name = "Name"
                                        }
                                    },
                                    RestartPolicy = "Always"
                                }
                            }
                        },
                        Status = new V1JobStatus
                        {
                            Active = 1,
                            CompletedIndexes = "1",
                            Conditions = new List<V1JobCondition>
                            {
                                new()
                                {
                                    Status = "True",
                                    Type = "Type",
                                    Reason = "Reason",
                                    Message = "Message"
                                }
                            },
                            Failed = 1,
                            StartTime = DateTime.MinValue,
                            CompletionTime = DateTime.MinValue,
                            Succeeded = 1
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Completions, Duration, Images, Containers, Age from #kubernetes.jobs()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual(1, table[0][2]);
        Assert.AreEqual(TimeSpan.Zero, table[0][3]);
        Assert.AreEqual("Image", table[0][4]);
        Assert.AreEqual("Name", table[0][5]);
        Assert.AreEqual(DateTime.MinValue, table[0][6]);
        
        Assert.AreEqual(7, table[0].Count);
    }
    
    [TestMethod]
    public void WhenPersistentVolumeClaimsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListPersistentVolumeClaimsForAllNamespaces())
            .Returns(new V1PersistentVolumeClaimList
            {
                Items = new List<V1PersistentVolumeClaim>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            NamespaceProperty = "Namespace",
                            CreationTimestamp = DateTime.MinValue,
                        },
                        Spec = new V1PersistentVolumeClaimSpec
                        {
                            AccessModes = new List<string>
                            {
                                "AccessModes"
                            },
                            VolumeName = "VolumeName",
                            VolumeMode = "VolumeMode",
                            StorageClassName = "StorageClassName",
                            Resources = new V1ResourceRequirements
                            {
                                Requests = new Dictionary<string, ResourceQuantity>
                                {
                                    {"Key", new ResourceQuantity("5")}
                                }
                            }
                        },
                        Status = new V1PersistentVolumeClaimStatus
                        {
                            Phase = "Phase",
                            AccessModes = new List<string>
                            {
                                "AccessModes"
                            },
                            Capacity = new Dictionary<string, ResourceQuantity>
                            {
                                {"Key", new ResourceQuantity("3")}
                            }
                        }
                    }
                }
            });
        
        var query = "select Namespace, Name, Capacity, Volume, Status, Age from #kubernetes.persistentvolumeclaims()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Namespace", table[0][0]);
        Assert.AreEqual("Name", table[0][1]);
        Assert.AreEqual("3", table[0][2]);
        Assert.AreEqual("VolumeName", table[0][3]);
        Assert.AreEqual("Phase", table[0][4]);
        Assert.AreEqual(DateTime.MinValue, table[0][5]);
        
        Assert.AreEqual(6, table[0].Count);
    }

    [TestMethod]
    public void WhenPersistentVolumesQueries_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListPersistentVolumes())
            .Returns(new V1PersistentVolumeList
            {
                Items = new List<V1PersistentVolume>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            CreationTimestamp = DateTime.MinValue,
                            NamespaceProperty = "Namespace"
                        },
                        Spec = new V1PersistentVolumeSpec
                        {
                            Capacity = new Dictionary<string, ResourceQuantity>
                            {
                                {"Key", new ResourceQuantity("3")}
                            },
                            AccessModes = new List<string>
                            {
                                "AccessModes"
                            },
                            PersistentVolumeReclaimPolicy = "PersistentVolumeReclaimPolicy",
                            StorageClassName = "StorageClassName",
                            VolumeMode = "VolumeMode",
                            ClaimRef = new V1ObjectReference
                            {
                                Name = "Name",
                                NamespaceProperty = "Namespace"
                            }
                        },
                        Status = new V1PersistentVolumeStatus
                        {
                            Phase = "Phase",
                            Reason = "Reason",
                            Message = "Message"
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, AccessModes, ReclaimPolicy, Status, Claim, StorageClass, Reason, Age from #kubernetes.persistentvolumes()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("AccessModes", table[0][2]);
        Assert.AreEqual("PersistentVolumeReclaimPolicy", table[0][3]);
        Assert.AreEqual("Phase", table[0][4]);
        Assert.AreEqual("Name", table[0][5]);
        Assert.AreEqual("StorageClassName", table[0][6]);
        Assert.AreEqual("Reason", table[0][7]);
        Assert.AreEqual(DateTime.MinValue, table[0][8]);
        
        Assert.AreEqual(9, table[0].Count);
    }

    [TestMethod]
    public void WhenReplicaSetsQueried_ShouldReturnValue()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListReplicaSetsForAllNamespaces())
            .Returns(new V1ReplicaSetList
            {
                Items = new List<V1ReplicaSet>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            CreationTimestamp = DateTime.MinValue,
                            NamespaceProperty = "Namespace"
                        },
                        Spec = new V1ReplicaSetSpec
                        {
                            Replicas = 1,
                            Selector = new V1LabelSelector
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    {"Key", "Value"}
                                }
                            },
                            Template = new V1PodTemplateSpec
                            {
                                Spec = new V1PodSpec
                                {
                                    Containers = new List<V1Container>
                                    {
                                        new()
                                        {
                                            Image = "Image",
                                            ImagePullPolicy = "ImagePullPolicy",
                                            Name = "Name"
                                        }
                                    },
                                    RestartPolicy = "Always"
                                }
                            }
                        },
                        Status = new V1ReplicaSetStatus
                        {
                            Replicas = 1,
                            AvailableReplicas = 1,
                            FullyLabeledReplicas = 1,
                            ObservedGeneration = 1,
                            ReadyReplicas = 1
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Desired, Current, Ready, Age from #kubernetes.replicasets()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);

        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual(1, table[0][2]);
        Assert.AreEqual(1, table[0][3]);
        Assert.AreEqual(1, table[0][4]);
        Assert.AreEqual(DateTime.MinValue, table[0][5]);
        
        Assert.AreEqual(6, table[0].Count);
    }

    [TestMethod]
    public void WhenSecretDataQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        var keyBytes = new byte[] {1, 2, 3};
        
        api.Setup(f => f.ListSecretsForAllNamespaces())
            .Returns(new V1SecretList
            {
                Items = new List<V1Secret>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            CreationTimestamp = DateTime.MinValue,
                            NamespaceProperty = "Namespace"
                        },
                        Type = "Type",
                        Data = new Dictionary<string, byte[]>
                        {
                            {"Key", keyBytes}
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Key, Value from #kubernetes.secretsdata()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);

        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("Key", table[0][2]);
        Assert.AreEqual(keyBytes, table[0][3]);
        
        Assert.AreEqual(4, table[0].Count);
    }

    [TestMethod]
    public void WhenSecretsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListSecretsForAllNamespaces())
            .Returns(new V1SecretList
            {
                Items = new List<V1Secret>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            CreationTimestamp = DateTime.MinValue,
                            NamespaceProperty = "Namespace"
                        },
                        Type = "Type",
                        Data = new Dictionary<string, byte[]>
                        {
                            {"Key", new byte[] {1, 2, 3}}
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Type, Age from #kubernetes.secrets()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);

        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("Type", table[0][2]);
        Assert.AreEqual(DateTime.MinValue, table[0][3]);
        
        Assert.AreEqual(4, table[0].Count);
    }

    [TestMethod]
    public void WhenStatefulSetsQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListStatefulSetsForAllNamespaces())
            .Returns(new V1StatefulSetList
            {
                Items = new List<V1StatefulSet>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            CreationTimestamp = DateTime.MinValue,
                            NamespaceProperty = "Namespace"
                        },
                        Spec = new V1StatefulSetSpec
                        {
                            Replicas = 1,
                            Selector = new V1LabelSelector
                            {
                                MatchLabels = new Dictionary<string, string>
                                {
                                    {"Key", "Value"}
                                }
                            },
                            Template = new V1PodTemplateSpec
                            {
                                Spec = new V1PodSpec
                                {
                                    Containers = new List<V1Container>
                                    {
                                        new()
                                        {
                                            Image = "Image",
                                            ImagePullPolicy = "ImagePullPolicy",
                                            Name = "Name"
                                        }
                                    },
                                    RestartPolicy = "Always"
                                }
                            }
                        },
                        Status = new V1StatefulSetStatus
                        {
                            Replicas = 1,
                            ReadyReplicas = 1,
                            CurrentReplicas = 1,
                            CurrentRevision = "CurrentRevision",
                            UpdateRevision = "UpdateRevision",
                            ObservedGeneration = 1
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, Replicas, Age from #kubernetes.statefulsets()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);

        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual(1, table[0][2]);
        Assert.AreEqual(DateTime.MinValue, table[0][3]);
        
        Assert.AreEqual(4, table[0].Count);
    }

    [TestMethod]
    public void WhenPodContainersQueried_ShouldReturnValues()
    {
        var api = new Mock<IKubernetesApi>();
        
        api.Setup(f => f.ListPodsForAllNamespaces())
            .Returns(new V1PodList
            {
                Items = new List<V1Pod>
                {
                    new()
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = "Name",
                            CreationTimestamp = DateTime.MinValue,
                            NamespaceProperty = "Namespace",
                            Labels = new Dictionary<string, string>
                            {
                                {"name", "PodName"}
                            }
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new()
                                {
                                    Image = "Image",
                                    ImagePullPolicy = "ImagePullPolicy",
                                    Name = "ContainerName"
                                }
                            },
                            RestartPolicy = "Always"
                        },
                        Status = new V1PodStatus
                        {
                            Phase = "Phase",
                            StartTime = DateTime.MinValue,
                            ContainerStatuses = new List<V1ContainerStatus>
                            {
                                new()
                                {
                                    Image = "Image",
                                    ImageID = "ImageID",
                                    Name = "Name",
                                    Ready = true,
                                    RestartCount = 1,
                                    Started = true,
                                    State = new V1ContainerState
                                    {
                                        Running = new V1ContainerStateRunning
                                        {
                                            StartedAt = DateTime.MinValue
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        
        var query = "select Name, Namespace, ContainerName, Image, ImagePullPolicy, Age from #kubernetes.podcontainers()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual("Name", table[0][0]);
        Assert.AreEqual("Namespace", table[0][1]);
        Assert.AreEqual("ContainerName", table[0][2]);
        Assert.AreEqual("Image", table[0][3]);
        Assert.AreEqual("ImagePullPolicy", table[0][4]);
        Assert.AreEqual(DateTime.MinValue, table[0][5]);
        
        Assert.AreEqual(6, table[0].Count);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, IKubernetesApi api)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();

        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new KubernetesSchema(api));
        
        return InstanceCreator.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            mockSchemaProvider.Object, 
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                {0, new Dictionary<string, string>
                {
                    {"MUSOQ_AIRTABLE_API_KEY", "NOPE"},
                    {"MUSOQ_AIRTABLE_BASE_ID", "NOPE x2"}
                }}
            });
    }

    static KubernetesTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}