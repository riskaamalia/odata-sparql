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
    using System.Diagnostics;
#if !WINDOWS_PHONE && !SILVERLIGHT
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;
    using System.Security.Permissions;
#endif
    #endregion Namespaces

    /// <summary>
    /// Exception type representing an in-stream error parsed when reading a payload.
    /// </summary>
#if !WINDOWS_PHONE && !SILVERLIGHT
    [Serializable]
#endif
#if !ORCAS && !WINDOWS_PHONE && !SILVERLIGHT
    [SuppressMessage("Microsoft.Design", "CA1032", Justification = "No need for serialization constructor, follows ISafeSerializationData info pattern.")]
#endif
    [DebuggerDisplay("{Message}")]
    public sealed class ODataErrorException : ODataException
    {
        /// <summary>The <see cref="ODataErrorExceptionSafeSerializationState"/> value containing <see cref="ODataError"/> instance representing the error
        /// read from the payload.
        /// </summary>
#if !ORCAS && !WINDOWS_PHONE && !SILVERLIGHT
        // Because we don't want the exception state to be serialized normally, we take care of that in the constructor.
        [NonSerialized]
#endif
        private ODataErrorExceptionSafeSerializationState state;

        /// <summary>
        /// Initializes a new instance of the ODataErrorException class.
        /// </summary>
        /// <remarks>
        /// The Message property is initialized to a system-supplied message 
        /// that describes the error. This message takes into account the 
        /// current system culture. The Error property will be initialized with an empty <see cref="ODataError"/> instance.
        /// </remarks>
        public ODataErrorException()
            : this(Strings.ODataErrorException_GeneralError)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ODataException class.
        /// </summary>
        /// <param name="message">Plain text error message for this exception.</param>
        /// <remarks>
        /// The Error property will be initialized with an empty <see cref="ODataError"/> instance.
        /// </remarks>
        public ODataErrorException(string message)
            : this(message, (Exception)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ODataException class.
        /// </summary>
        /// <param name="message">Plain text error message for this exception.</param>
        /// <param name="innerException">Exception that caused this exception to be thrown.</param>
        /// <remarks>
        /// The Error property will be initialized with an empty <see cref="ODataError"/> instance.
        /// </remarks>
        public ODataErrorException(string message, Exception innerException)
            : this(message, innerException, new ODataError())
        {
        }

        /// <summary>
        /// Initializes a new instance of the ODataErrorException class.
        /// </summary>
        /// <param name="error">The <see cref="ODataError"/> instance representing the error read from the payload.</param>
        /// <remarks>
        /// The Message property is initialized to a system-supplied message 
        /// that describes the error. This message takes into account the 
        /// current system culture.
        /// </remarks>
        public ODataErrorException(ODataError error)
            : this(Strings.ODataErrorException_GeneralError, null, error)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ODataException class.
        /// </summary>
        /// <param name="message">Plain text error message for this exception.</param>
        /// <param name="error">The <see cref="ODataError"/> instance representing the error read from the payload.</param>
        public ODataErrorException(string message, ODataError error)
            : this(message, null, error)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ODataException class.
        /// </summary>
        /// <param name="message">Plain text error message for this exception.</param>
        /// <param name="innerException">Exception that caused this exception to be thrown.</param>
        /// <param name="error">The <see cref="ODataError"/> instance representing the error read from the payload.</param>
        public ODataErrorException(string message, Exception innerException, ODataError error)
            : base(message, innerException)
        {
            this.state.ODataError = error;

#if !ORCAS && !WINDOWS_PHONE && !SILVERLIGHT
            // In response to SerializeObjectState, we need to provide any state to serialize with the exception.
            // In this case, since our state is already stored in an ISafeSerializationData implementation,
            // we can just provide that.
            this.SerializeObjectState += delegate(object exception, SafeSerializationEventArgs eventArgs)
            {
                eventArgs.AddSerializedState(this.state);
            };
#endif
        }

#if ORCAS
#pragma warning disable 0628
        // Warning CS0628:
        // A sealed class cannot introduce a protected member because no other class will be able to inherit from the 
        // sealed class and use the protected member.
        //
        // This method is used by the runtime when deserializing an exception.

        /// <summary>
        /// Initializes a new instance of the ODataErrorException class from the 
        /// specified SerializationInfo and StreamingContext instances.
        /// </summary>
        /// <param name="info">
        /// A SerializationInfo containing the information required to serialize 
        /// the new ODataException.
        /// </param>
        /// <param name="context">
        /// A StreamingContext containing the source of the serialized stream 
        /// associated with the new ODataErrorException.
        /// </param>
        [SuppressMessage("Microsoft.Design", "CA1047", Justification = "Follows serialization info pattern.")]
        [SuppressMessage("Microsoft.Design", "CA1032", Justification = "Follows serialization info pattern.")]
        protected ODataErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ExceptionUtils.CheckArgumentNotNull(info, "serializationInfo");

            this.state.ODataError = (ODataError)info.GetValue("Error", typeof(ODataError));
        }
#pragma warning restore 0628
#endif

        /// <summary>
        /// The <see cref="ODataError"/> instance describing the in-stream error represented by this exception.
        /// </summary>
        public ODataError Error
        {
            get
            {
                return this.state.ODataError;
            }
        }

#if ORCAS
        /// <summary>
        /// Recreates the <see cref="ODataError"/> instance of the exception.
        /// </summary>
        /// <param name="info">
        /// A SerializationInfo containing the information required to serialize 
        /// the ODataErrorException.
        /// </param>
        /// <param name="context">
        /// A StreamingContext containing the source of the serialized stream 
        /// associated with the new ODataErrorException.
        /// </param>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ExceptionUtils.CheckArgumentNotNull(info, "serializationInfo");

            base.GetObjectData(info, context);
            info.AddValue("Error", this.state.ODataError);
        }
#endif

        /// <summary>
        /// Implement the ISafeSerializationData interface to contain custom exception data in a partially trusted assembly.
        /// Use this interface in post-ORCAS to replace the Exception.GetObjectData method, which is marked with the SecurityCriticalAttribute.
        /// </summary>
#if !WINDOWS_PHONE && !SILVERLIGHT
        [Serializable]
#endif
        private struct ODataErrorExceptionSafeSerializationState
#if !ORCAS && !WINDOWS_PHONE && !SILVERLIGHT
            : ISafeSerializationData
#endif
        {
            /// <summary>
            /// Gets or sets the <see cref="ODataError"/> object.
            /// </summary>
            public ODataError ODataError
            {
                get;
                set;
            }

#if !ORCAS && !WINDOWS_PHONE && !SILVERLIGHT
            /// <summary>
            /// This method is called when deserialization of the exception is complete.
            /// </summary>
            /// <param name="obj">The exception object.</param>
            void ISafeSerializationData.CompleteDeserialization(object obj)
            {
                // Since the exception simply contains an instance of the exception state object, we can repopulate it 
                // here by just setting its instance field to be equal to this deserialized state instance.
                ODataErrorException exception = obj as ODataErrorException;
                exception.state = this;
            }
#endif
        }
    }
}
