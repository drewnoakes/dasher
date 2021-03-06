#region License
//
// Dasher
//
// Copyright 2015-2017 Drew Noakes
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dasher.Contracts.Types
{
    /// <summary>
    /// Contract to use when writing a hetrogenous list of values.
    /// </summary>
    public sealed class ListWriteContract : ByValueContract, IWriteContract
    {
        internal static bool CanProcess(Type type) => type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

        /// <summary>
        /// The contract to use when writing list items.
        /// </summary>
        public IWriteContract ItemContract { get; }

        internal ListWriteContract(Type type, ContractCollection contractCollection)
        {
            if (!CanProcess(type))
                throw new ArgumentException($"Type {type} must be {nameof(IReadOnlyList<int>)}<>.", nameof(type));
            ItemContract = contractCollection.GetOrAddWriteContract(type.GetGenericArguments().Single());
        }

        internal ListWriteContract(IWriteContract itemContract) => ItemContract = itemContract;

        /// <inheritdoc />
        public override bool Equals(Contract other)
        {
            return other is ListWriteContract o && ((Contract)o.ItemContract).Equals((Contract)ItemContract);
        }

        /// <inheritdoc />
        protected override int ComputeHashCode() => unchecked((int)0xA4A76926 ^ ItemContract.GetHashCode());

        /// <inheritdoc />
        public override IEnumerable<Contract> Children => new[] { (Contract)ItemContract };

        /// <inheritdoc />
        public override string MarkupValue => $"{{list {ItemContract.ToReferenceString()}}}";

        /// <inheritdoc />
        public IWriteContract CopyTo(ContractCollection collection)
        {
            return collection.GetOrCreate(this, () => new ListWriteContract(ItemContract.CopyTo(collection)));
        }
    }

    /// <summary>
    /// Contract to use when reading a hetrogenous list of values.
    /// </summary>
    public sealed class ListReadContract : ByValueContract, IReadContract
    {
        internal static bool CanProcess(Type type) => ListWriteContract.CanProcess(type);

        /// <summary>
        /// The contract to use when reading list items.
        /// </summary>
        public IReadContract ItemContract { get; }

        internal ListReadContract(Type type, ContractCollection contractCollection)
        {
            if (!CanProcess(type))
                throw new ArgumentException($"Type {type} must be {nameof(IReadOnlyList<int>)}<>.", nameof(type));
            ItemContract = contractCollection.GetOrAddReadContract(type.GetGenericArguments().Single());
        }

        internal ListReadContract(IReadContract itemContract) => ItemContract = itemContract;

        /// <inheritdoc />
        public bool CanReadFrom(IWriteContract writeContract, bool strict)
        {
            return writeContract is ListWriteContract ws && ItemContract.CanReadFrom(ws.ItemContract, strict);
        }

        /// <inheritdoc />
        public override bool Equals(Contract other) => other is ListReadContract lrc && lrc.ItemContract.Equals(ItemContract);

        /// <inheritdoc />
        protected override int ComputeHashCode() => unchecked((int)0x9ABCF854 ^ ItemContract.GetHashCode());

        /// <inheritdoc />
        public override IEnumerable<Contract> Children => new[] { (Contract)ItemContract };

        /// <inheritdoc />
        public override string MarkupValue => $"{{list {ItemContract.ToReferenceString()}}}";

        /// <inheritdoc />
        public IReadContract CopyTo(ContractCollection collection)
        {
            return collection.GetOrCreate(this, () => new ListReadContract(ItemContract.CopyTo(collection)));
        }
    }
}
