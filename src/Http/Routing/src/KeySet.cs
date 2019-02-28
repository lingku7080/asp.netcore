// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Routing
{
    internal readonly struct KeySet : IEquatable<KeySet>
    {
        public readonly IReadOnlyList<string> Keys;

        public KeySet(IReadOnlyList<string> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            Keys = keys;
        }

        public bool Equals(KeySet other)
        {
            if (Keys.Count != other.Keys.Count)
            {
                return false;
            }

            for (var i = 0; i < Keys.Count; i++)
            {
                if (!string.Equals(Keys[i], other.Keys[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            for (var i = 0; i < Keys.Count; i++)
            {
                hash.Add(Keys[i], StringComparer.OrdinalIgnoreCase);
            }

            return hash.ToHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is KeySet other)
            {
                return Equals(other);
            }

            return false;
        }
    }
}
