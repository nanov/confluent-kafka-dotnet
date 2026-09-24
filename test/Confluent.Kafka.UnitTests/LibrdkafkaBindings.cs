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
using System.Reflection;
using Confluent.Kafka.Impl;
using Xunit;


namespace Confluent.Kafka.UnitTests
{
    public class LibrdkafkaBindingsTests
    {
        /// <summary>
        ///     On net8.0+ the librdkafka delegate fields are bound without
        ///     reflection by the generated SetDelegates() (see
        ///     scripts/gen-aot-bindings.py). A native function added upstream
        ///     that is missing from the generated binding would otherwise
        ///     surface as a NullReferenceException at first use; this test
        ///     turns it into a build-time failure.
        /// </summary>
        [Fact]
        public void AllDelegateFieldsAreBound()
        {
            Library.Load();

            var unbound = new List<string>();
            foreach (var field in typeof(Librdkafka).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }
                if (field.GetValue(null) == null)
                {
                    unbound.Add(field.Name);
                }
            }

            Assert.True(unbound.Count == 0,
                "Unbound librdkafka delegate fields (re-run scripts/gen-aot-bindings.py): "
                + string.Join(", ", unbound));
        }
    }
}
