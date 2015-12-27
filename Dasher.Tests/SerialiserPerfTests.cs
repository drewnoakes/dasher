#region License
//
// Dasher
//
// Copyright 2015 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/dasher
//
#endregion

using System;
using System.Diagnostics;
using System.IO;
using MsgPack;
using MsgPack.Serialization;
using Xunit;

namespace Dasher.Tests
{
    public sealed class SerialiserPerfTests
    {
        [Fact]
        public void SerialisationPerf()
        {
            var dasherSer = new Serialiser<UserScore>();
            var dasherPacker = new UnsafePacker(Stream.Null);

            var cliSer = MessagePackSerializer.Get<UserScore>(new SerializationContext
            {
                CompatibilityOptions =
                {
                    PackerCompatibilityOptions = PackerCompatibilityOptions.None
                },
                SerializationMethod = SerializationMethod.Map
            });
            var cliPacker = MsgPack.Packer.Create(Stream.Null, PackerCompatibilityOptions.None);

            var obj = new UserScore("Bob", 1234);

            const int warmUpLoopCount = 1000;
            const int timedLoopCount = 10 * 1000 * 1000;

            var sw = Stopwatch.StartNew();

            ////

            for (var i = 0; i < warmUpLoopCount; i++)
                cliSer.PackTo(cliPacker, obj);

            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForFullGCComplete(1000);
            GC.WaitForPendingFinalizers();

            sw.Restart();

            for (var i = 0; i < timedLoopCount; i++)
                cliSer.PackTo(cliPacker, obj);

            var cliMs = sw.Elapsed.TotalMilliseconds;

            ////

            for (var i = 0; i < warmUpLoopCount; i++)
            {
                dasherSer.Serialise(dasherPacker, obj);
                dasherPacker.Flush();
            }

            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForFullGCComplete(1000);
            GC.WaitForPendingFinalizers();

            sw.Restart();

            for (var i = 0; i < timedLoopCount; i++)
            {
                dasherSer.Serialise(dasherPacker, obj);
                dasherPacker.Flush();
            }

            var dasherMs = sw.Elapsed.TotalMilliseconds;

            ////

#if DEBUG
            Assert.True(false, "Performance comparison must be performed on a release build.");
#endif

            // serialisation performance is on par with MsgPack.Cli. should always be within 10%.
            Assert.True(dasherMs < cliMs * 1.1, $"{nameof(dasherMs)}={dasherMs} {nameof(cliMs)}={cliMs}");
        }
    }
}