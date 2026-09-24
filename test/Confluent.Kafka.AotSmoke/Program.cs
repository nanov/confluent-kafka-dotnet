// Copyright 2016-2023 Confluent Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Refer to LICENSE for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;


namespace Confluent.Kafka.AotSmoke
{
    /// <summary>
    ///     End-to-end smoke test for the Native AOT build of Confluent.Kafka:
    ///     loads librdkafka, produces N messages, consumes them back with both
    ///     the typed consumer and the raw (allocation-free) consumer, and
    ///     checks that the librdkafka -> managed callbacks (log, statistics,
    ///     rebalance, offset commit, delivery report) fire.
    ///
    ///     Usage: Confluent.Kafka.AotSmoke [bootstrap.servers] [message count]
    ///     (defaults: $KAFKA_BOOTSTRAP_SERVERS or localhost:9092, 100).
    /// </summary>
    public static class Program
    {
        private static int failures;

        private static void Check(bool condition, string what)
        {
            if (condition)
            {
                Console.WriteLine("  ok   " + what);
            }
            else
            {
                failures++;
                Console.WriteLine("  FAIL " + what);
            }
        }

        public static int Main(string[] args)
        {
            var bootstrapServers = args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
            var count = args.Length > 1 ? int.Parse(args[1]) : 100;
            var topic = "aot-smoke-" + Guid.NewGuid().ToString("N");

            Console.WriteLine("Confluent.Kafka Native AOT smoke test");
            Console.WriteLine("  bootstrap.servers: " + bootstrapServers);
            Console.WriteLine("  topic:             " + topic);

            Console.WriteLine("Library");
            Check(Library.Load() || Library.IsLoaded, "Library.Load()");
            Check(Library.Version != 0, "Library.Version = 0x" + Library.Version.ToString("x"));
            Check(!string.IsNullOrEmpty(Library.VersionString), "Library.VersionString = " + Library.VersionString);
            Check(Library.DebugContexts.Length > 0, "Library.DebugContexts (" + Library.DebugContexts.Length + ")");

            Produce(bootstrapServers, topic, count);
            ConsumeTyped(bootstrapServers, topic, count);
            ConsumeRaw(bootstrapServers, topic, count);

            Console.WriteLine("Shutdown");
            Check(Library.HandleCount == 0, "Library.HandleCount == 0 (" + Library.HandleCount + ")");

            Console.WriteLine(failures == 0 ? "PASSED" : "FAILED (" + failures + ")");
            return failures == 0 ? 0 : 1;
        }

        private static void Produce(string bootstrapServers, string topic, int count)
        {
            Console.WriteLine("Producer");
            var logs = 0;
            var stats = 0;
            var delivered = 0;
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                StatisticsIntervalMs = 100,
                Debug = "broker",
            };

            using (var producer = new ProducerBuilder<string, string>(config)
                .SetLogHandler((_, __) => Interlocked.Increment(ref logs))
                .SetStatisticsHandler((_, __) => Interlocked.Increment(ref stats))
                .Build())
            {
                for (var i = 0; i < count; i++)
                {
                    producer.Produce(topic,
                        new Message<string, string>
                        {
                            Key = "key-" + i,
                            Value = "value-" + i,
                            Headers = new Headers { { "h", Encoding.UTF8.GetBytes(i.ToString()) } }
                        },
                        dr =>
                        {
                            if (dr.Error.IsError)
                            {
                                Console.WriteLine("  delivery error: " + dr.Error.Reason);
                            }
                            else
                            {
                                Interlocked.Increment(ref delivered);
                            }
                        });
                }
                var remaining = producer.Flush(TimeSpan.FromSeconds(30));
                Check(remaining == 0, "Flush (remaining " + remaining + ")");
            }

            Check(delivered == count, "delivery reports (" + delivered + "/" + count + ")");
            Check(logs > 0, "log callback (" + logs + ")");
            Check(stats > 0, "statistics callback (" + stats + ")");
        }

        private static ConsumerConfig ConsumerConfig(string bootstrapServers)
            => new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = "aot-smoke-" + Guid.NewGuid().ToString("N"),
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                AutoCommitIntervalMs = 100,
                StatisticsIntervalMs = 100,
                Debug = "consumer",
                EnablePartitionEof = true,
            };

        private static void ConsumeTyped(string bootstrapServers, string topic, int count)
        {
            Console.WriteLine("Consumer<string, string>");
            var logs = 0;
            var stats = 0;
            var assigned = 0;
            var revoked = 0;
            var committed = 0;
            var consumed = 0;
            var eof = false;
            var seen = new HashSet<string>();
            ConsumeResult<string, string> last = null;

            using (var consumer = new ConsumerBuilder<string, string>(ConsumerConfig(bootstrapServers))
                .SetLogHandler((_, __) => Interlocked.Increment(ref logs))
                .SetStatisticsHandler((_, __) => Interlocked.Increment(ref stats))
                .SetPartitionsAssignedHandler((_, partitions) => { assigned += partitions.Count; })
                .SetPartitionsRevokedHandler((_, partitions) => { revoked += partitions.Count; })
                .SetOffsetsCommittedHandler((_, offsets) => { if (!offsets.Error.IsError) committed++; })
                .Build())
            {
                consumer.Subscribe(topic);
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (DateTime.UtcNow < deadline && !(eof && consumed >= count))
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    if (result == null)
                    {
                        continue;
                    }
                    if (result.IsPartitionEOF)
                    {
                        eof = true;
                        continue;
                    }
                    consumed++;
                    last = result;
                    seen.Add(result.Message.Key);
                    if (result.Message.Value != "value-" + result.Message.Key.Substring(4))
                    {
                        failures++;
                        Console.WriteLine("  FAIL value mismatch for " + result.Message.Key);
                    }
                    if (result.Message.Headers.Count != 1)
                    {
                        failures++;
                        Console.WriteLine("  FAIL headers missing for " + result.Message.Key);
                    }
                }

                // Give the auto commit a chance to fire before closing.
                Thread.Sleep(300);
                consumer.Consume(TimeSpan.FromMilliseconds(100));

                // Synchronous commit of an explicit offset exercises rd_kafka_commit_queue.
                if (last != null)
                {
                    consumer.Commit(last);
                    var committedOffsets = consumer.Committed(new[] { last.TopicPartition }, TimeSpan.FromSeconds(10));
                    Check(committedOffsets.Count == 1 && committedOffsets[0].Offset == last.Offset + 1,
                        "synchronous Commit(result) (" + (committedOffsets.Count > 0 ? committedOffsets[0].Offset.ToString() : "none") + ")");
                }
                else
                {
                    Check(false, "synchronous Commit(result) - nothing consumed");
                }

                consumer.Close();
            }

            Check(consumed == count && seen.Count == count, "consumed (" + consumed + "/" + count + ", distinct " + seen.Count + ")");
            Check(eof, "partition EOF");
            Check(assigned > 0, "rebalance assigned callback (" + assigned + ")");
            Check(revoked > 0, "rebalance revoked callback (" + revoked + ")");
            Check(committed > 0, "offset commit callback (" + committed + ")");
            Check(logs > 0, "log callback (" + logs + ")");
            Check(stats > 0, "statistics callback (" + stats + ")");
        }

        private static void ConsumeRaw(string bootstrapServers, string topic, int count)
        {
            Console.WriteLine("RawConsumer");
            var stats = 0;
            var consumed = 0;
            var headers = 0;
            var eof = false;
            long lastOffset = -1;

            using (var consumer = new RawConsumerBuilder(ConsumerConfig(bootstrapServers))
                .SetStatisticsHandler(_ => Interlocked.Increment(ref stats))
                .BuildRaw())
            {
                consumer.Subscribe(topic);
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (DateTime.UtcNow < deadline && !(eof && consumed >= count))
                {
                    using (var msg = consumer.ConsumeRaw(500))
                    {
                        if (msg.IsEmpty)
                        {
                            continue;
                        }
                        if (msg.IsPartitionEOF)
                        {
                            eof = true;
                            continue;
                        }
                        if (msg.ErrorCode != ErrorCode.NoError)
                        {
                            failures++;
                            Console.WriteLine("  FAIL consume error " + msg.ErrorCode);
                            continue;
                        }
                        consumed++;
                        lastOffset = msg.Offset;
                        if (!msg.Key.StartsWith("key-"u8) || !msg.Value.StartsWith("value-"u8))
                        {
                            failures++;
                            Console.WriteLine("  FAIL unexpected raw key/value at offset " + msg.Offset);
                        }
                        foreach (var _ in msg.Headers)
                        {
                            headers++;
                        }
                    }
                }
                consumer.Close();
            }

            Check(consumed == count, "consumed (" + consumed + "/" + count + ", last offset " + lastOffset + ")");
            Check(headers == count, "headers (" + headers + "/" + count + ")");
            Check(eof, "partition EOF");
            Check(stats > 0, "raw statistics callback (" + stats + ")");
        }
    }
}
