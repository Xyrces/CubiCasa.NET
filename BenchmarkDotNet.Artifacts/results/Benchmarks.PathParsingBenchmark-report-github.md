```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3


```
| Method      | Mean     | Error   | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------ |---------:|--------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| Original    | 709.3 μs | 6.99 μs | 11.86 μs |  1.00 |    0.02 | 22.4609 | 14.6484 | 537.92 KB |        1.00 |
| LibraryCall | 423.1 μs | 1.81 μs |  1.61 μs |  0.60 |    0.01 |  4.3945 |  0.9766 | 110.73 KB |        0.21 |
