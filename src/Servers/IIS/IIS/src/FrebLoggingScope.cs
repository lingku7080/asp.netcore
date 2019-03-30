using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.AspNetCore.Server.IIS
{
    public class FrebLoggingScope : IReadOnlyList<KeyValuePair<string, object>>
    {
        private IntPtr pInProcessHandler;

        public FrebLoggingScope(IntPtr pInProcessHandler)
        {
            this.pInProcessHandler = pInProcessHandler;
        }

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new KeyValuePair<string, object>("InProcessHandler", pInProcessHandler);
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public int Count => 1;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (int i = 0; i < Count; ++i)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
