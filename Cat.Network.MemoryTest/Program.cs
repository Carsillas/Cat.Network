
using BenchmarkDotNet.Running;
using Cat.Network.Test.Serialization;
using Cat.Network.Test;
using Cat.Network;

var summary = BenchmarkRunner.Run<MemoryTest>();