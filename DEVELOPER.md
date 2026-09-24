# Developer Notes

This document provides information useful to developers working on confluent-kafka-dotnet.


## Building

Nuget packages are built automatically by Semaphore CI corresponding to every commit to a PR or master branch as well as release tags. For further details, inspect the [.semaphore/semaphore.yml](.semaphore/semaphore.yml) file.


## Tests

### Unit Tests

There are unit test suites corresponding to each nuget package. These are [Confluent.Kafka.UnitTests](test/Confluent.Kafka.UnitTests), 
[Confluent.SchemaRegistry.UnitTests](test/Confluent.SchemaRegistry.UnitTests) and
[Confluent.SchemaRegistry.Serdes.UnitTests](test/Confluent.SchemaRegistry.Serdes.UnitTests). To execute, enter the
relevant directory and run:

```
dotnet test
```

### Integration Tests

From the test/docker directory bring up the Kafka cluster with two schema registry instances (one with basic auth enabled, one without).

```
docker-compose up
```

There are integration test suites corresponding to each nuget package. These are [Confluent.Kafka.IntegrationTests](test/Confluent.Kafka.IntegrationTests), 
[Confluent.SchemaRegistry.IntegrationTests](test/Confluent.SchemaRegistry.IntegrationTests) and
[Confluent.SchemaRegistry.Serdes.IntegrationTests](test/Confluent.SchemaRegistry.Serdes.IntegrationTests).

To execute, enter the relevant directory and run:

```
dotnet test
```


## Native AOT

On `net8.0` and later `Confluent.Kafka` is trim and Native AOT compatible
(`IsAotCompatible=true`). `netstandard2.0` and `net462` keep the original
reflection based binding untouched.

### How the net8.0+ binding works

| Upstream (netstandard2.0 / net462) | net8.0+ |
| --- | --- |
| Three copy/pasted `DllImport` classes, one per DllName (`Impl/NativeMethods/NativeMethods{,_Alpine,_Centos8}.cs`) | One source-generated `[LibraryImport]` class, `Impl/NativeMethods/NativeMethods.LibraryImport.cs` (generated, see below) |
| `Librdkafka.SetDelegates(Type)` binds every function by reflection | `Librdkafka.SetDelegates()` (generated, `Impl/LibRdKafka.Modern.SetDelegates.cs`) assigns the same delegate fields from method groups |
| DllName selected by trying each class in turn | `NativeLibrary.SetDllImportResolver` in `Impl/LibRdKafka.Modern.cs` maps `librdkafka` to `librdkafka` / `alpine-librdkafka` / `centos8-librdkafka` or the path given to `Library.Load(path)` |
| Managed delegates marshalled to librdkafka callbacks | The upstream `conf_set_*_cb(conf, delegate)` calls are unchanged; on net8.0+ the `Librdkafka.Adapters` behind them ignore the delegate and register static `[UnmanagedCallersOnly]` entry points (`Impl/NativeCallbacks.cs`) that reach the client through the librdkafka opaque (a weak `GCHandle`, see `Impl/INativeCallbackTarget.cs`) |
| `Marshal.PtrToStructure` on structs with `string` fields | Blittable structs read in place with `Util.Marshal.ReadStruct` |

The wrappers in `Impl/LibRdKafka.cs` are shared by both bindings; the file only
gained `partial`, `#if` guards around the reflection loader and direct calls for
the per-message hot path (`consumer_poll`, `message_*`, `header_get_all`, ...).
The MSBuild side lives in `src/Confluent.Kafka/Directory.Build.targets`, so
`Confluent.Kafka.csproj` carries no diff against upstream.

### Adding a librdkafka function (fork workflow / syncing with upstream)

1. Add the `DllImport` / delegate field / wrapper exactly as upstream does
   (or merge upstream's change).
2. Run `python3 scripts/gen-aot-bindings.py`. It regenerates
   `NativeMethods.LibraryImport.cs` (imports, per-distro DllName constants and
   probe lists) and `LibRdKafka.Modern.SetDelegates.cs` (bindings plus the
   adapters for `StringBuilder` / callback-delegate signatures). Nothing else
   is required: a new callback works immediately through delegate marshalling
   (also AOT compatible); adding a `NativeCallbacks` trampoline for it is an
   optional optimization (add a hand written adapter of the same name to
   `Librdkafka.Adapters` in `Impl/LibRdKafka.Modern.cs` - it takes precedence).
3. `dotnet build` - `LibrdkafkaBindingsTests.AllDelegateFieldsAreBound` (unit
   tests) fails if a delegate field is left unbound on net8.0+.

Per-distro variants (`NativeMethods_<Variant>.cs`) renamed or added upstream
are picked up by the generated `AlpineDllNames` / `LinuxDllNames` probe lists;
only a change to upstream's *selection* logic (`LoadLinuxDelegates`) needs to
be mirrored in `NativeLibraryCandidates()` in `Impl/LibRdKafka.Modern.cs`.

Known exception: the AWS IAM auto-wire
(`Internal/OAuthBearer/Aws/AwsAutoWireDispatcher.cs`) uses reflection and emits
IL2026/IL2075 trim warnings; it is not supported in trimmed / AOT applications.

### AOT smoke test

`test/Confluent.Kafka.AotSmoke` is a `PublishAot=true` console application that
produces, consumes (typed and raw consumer) and checks that every callback fires.
AOT analysis warnings fail its publish. With a broker on `localhost:9092`
(e.g. `docker compose -f test/docker/docker-compose-kraft.yaml up -d`):

```
make aot-smoke                      # RID of the current machine
make aot-smoke RID=linux-x64 KAFKA_BOOTSTRAP_SERVERS=broker:9092
```
