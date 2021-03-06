//   Copyright 2011 Microsoft Corporation
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

namespace Microsoft.Data.OData
{
    #region Namespaces
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
#if ODATALIB_ASYNC
    using System.Threading.Tasks;
#endif
    #endregion Namespaces

    /// <summary>
    /// Wrapper class around an IODataRequestMessageAsync to isolate our code from the interface implementation.
    /// </summary>
    internal sealed class ODataRequestMessage : ODataMessage, 
#if ODATALIB_ASYNC
        IODataRequestMessageAsync
#else
        IODataRequestMessage
#endif
    {
        /// <summary>The request message this class is wrapping.</summary>
        private readonly IODataRequestMessage requestMessage;

        /// <summary>
        /// Constructs an internal wrapper around the <paramref name="requestMessage"/>
        /// that isolates the internal implementation of the ODataLib from the interface.
        /// </summary>
        /// <param name="requestMessage">The request message to wrap.</param>
        /// <param name="writing">true if the request message is being written; false when it is read.</param>
        /// <param name="disableMessageStreamDisposal">true if the stream returned should ignore dispose calls.</param>
        /// <param name="maxMessageSize">The maximum size of the message in bytes (or a negative value if no maximum applies).</param>
        internal ODataRequestMessage(IODataRequestMessage requestMessage, bool writing, bool disableMessageStreamDisposal, long maxMessageSize)
            : base(writing, disableMessageStreamDisposal, maxMessageSize)
        {
            DebugUtils.CheckNoExternalCallers();
            Debug.Assert(requestMessage != null, "requestMessage != null");

            this.requestMessage = requestMessage;
        }

        /// <summary>
        /// The request Url for this request message.
        /// </summary>
        public Uri Url
        {
            get
            {
                return this.requestMessage.Url;
            }

            set
            {
                throw new ODataException(Strings.ODataMessage_MustNotModifyMessage);
            }
        }

        /// <summary>
        /// The HTTP method used for this request message.
        /// </summary>
        public string Method
        {
            get
            {
                return this.requestMessage.Method;
            }

            set
            {
                throw new ODataException(Strings.ODataMessage_MustNotModifyMessage);
            }
        }

        /// <summary>
        /// Returns an enumerable over all the headers for this message.
        /// </summary>
        public override IEnumerable<KeyValuePair<string, string>> Headers
        {
            get
            {
                return this.requestMessage.Headers;
            }
        }

        /// <summary>
        /// Returns a value of an HTTP header.
        /// </summary>
        /// <param name="headerName">The name of the header to get.</param>
        /// <returns>The value of the HTTP header, or null if no such header was present on the message.</returns>
        public override string GetHeader(string headerName)
        {
            return this.requestMessage.GetHeader(headerName);
        }

        /// <summary>
        /// Sets the value of an HTTP header.
        /// </summary>
        /// <param name="headerName">The name of the header to set.</param>
        /// <param name="headerValue">The value of the HTTP header or 'null' if the header should be removed.</param>
        public override void SetHeader(string headerName, string headerValue)
        {
            this.VerifyCanSetHeader();
            this.requestMessage.SetHeader(headerName, headerValue);
        }

        /// <summary>
        /// Synchronously get the stream backing this message.
        /// </summary>
        /// <returns>The stream for this message.</returns>
        public override Stream GetStream()
        {
            return this.GetStream(this.requestMessage.GetStream, /*isRequest*/ true);
        }

#if ODATALIB_ASYNC
        /// <summary>
        /// Asynchronously get the stream backing this message.
        /// </summary>
        /// <returns>The stream for this message.</returns>
        public override Task<Stream> GetStreamAsync()
        {
            IODataRequestMessageAsync asyncRequestMessage = this.requestMessage as IODataRequestMessageAsync;
            if (asyncRequestMessage == null)
            {
                throw new ODataException(Strings.ODataRequestMessage_AsyncNotAvailable);
            }

            return this.GetStreamAsync(asyncRequestMessage.GetStreamAsync, /*isRequest*/ true);
        }
#endif
    }
}
