// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.Encodings.Web;

namespace Microsoft.AspNetCore.Razor.TagHelpers
{
    /// <summary>
    /// A <see cref="TextEncoder"/> that does not encode. Should not be used when writing directly to a response
    /// expected to contain valid HTML.
    /// </summary>
    public class NullHtmlEncoder : TextEncoder
    {
        /// <summary>
        /// Initializes a <see cref="NullHtmlEncoder"/> instance.
        /// </summary>
        protected NullHtmlEncoder()
        {
        }

        /// <summary>
        /// A <see cref="TextEncoder"/> instance that does not encode. Should not be used when writing directly to a
        /// response expected to contain valid HTML.
        /// </summary>
        public static NullHtmlEncoder Default { get; } = new NullHtmlEncoder();

        public override int MaxOutputCharactersPerInputCharacter => 1;

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            return -1;
        }

        /// <inheritdoc />
        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            numberOfCharactersWritten = 0;

            return false;
        }

        /// <inheritdoc />
        public override bool WillEncode(int unicodeScalar)
        {
            return false;
        }
    }
}
