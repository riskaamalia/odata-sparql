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
    using System.Diagnostics;
    using Microsoft.Data.Edm;
    #endregion Namespaces

    /// <summary>
    /// Base class for all OData serializers.
    /// </summary>
    internal abstract class ODataSerializer
    {
        /// <summary>
        /// The output context to write to.
        /// </summary>
        private readonly ODataOutputContext outputContext;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="outputContext">The output context to write to.</param>
        protected ODataSerializer(ODataOutputContext outputContext)
        {
            Debug.Assert(outputContext != null, "outputContext != null");

            this.outputContext = outputContext;
        }

        /// <summary>
        /// true if the WCF DS client compatibility format behavior should be used; otherwise false.
        /// </summary>
        internal bool UseClientFormatBehavior
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.UseClientFormatBehavior;
            }
        }

        /// <summary>
        /// true if the WCF DS server compatibility format behavior should be used; otherwise false.
        /// </summary>
        internal bool UseServerFormatBehavior
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.UseServerFormatBehavior;
            }
        }

        /// <summary>
        /// true if the default format behavior should be used; otherwise false.
        /// </summary>
        internal bool UseDefaultFormatBehavior
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.UseDefaultFormatBehavior;
            }
        }

        /// <summary>
        /// The message writer settings.
        /// </summary>
        internal ODataMessageWriterSettings MessageWriterSettings
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.MessageWriterSettings;
            }
        }

        /// <summary>
        /// The URL resolver.
        /// </summary>
        internal IODataUrlResolver UrlResolver
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.UrlResolver;
            }
        }

        /// <summary>
        /// The OData version of the output.
        /// </summary>
        internal ODataVersion Version
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.Version;
            }
        }

        /// <summary>
        /// true if the output is a response payload; false if it's a request payload.
        /// </summary>
        internal bool WritingResponse
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.WritingResponse;
            }
        }

        /// <summary>
        /// The model to use.
        /// </summary>
        internal IEdmModel Model
        {
            get
            {
                DebugUtils.CheckNoExternalCallers();
                return this.outputContext.Model;
            }
        }

        /// <summary>
        /// Creates a new instance of a duplicate property names checker.
        /// </summary>
        /// <returns>The newly created instance of duplicate property names checker.</returns>
        internal DuplicatePropertyNamesChecker CreateDuplicatePropertyNamesChecker()
        {
            DebugUtils.CheckNoExternalCallers();

            return new DuplicatePropertyNamesChecker(this.MessageWriterSettings.WriterBehavior.AllowDuplicatePropertyNames, this.WritingResponse);
        }

        /// <summary>
        /// Validates association link before writing.
        /// </summary>
        /// <param name="associationLink">The association link to validate.</param>
        /// <param name="entryEntityType">The entity type of the entry the association link belongs to.</param>
        protected void ValidateAssociationLink(ODataAssociationLink associationLink, IEdmEntityType entryEntityType)
        {
            Debug.Assert(associationLink != null, "associationLink != null");

            WriterValidationUtils.ValidateAssociationLink(associationLink, this.Version, this.WritingResponse);

            // We don't need the returned IEdmProperty since it was already validated to be a navigation property.
            WriterValidationUtils.ValidateNavigationPropertyDefined(associationLink.Name, entryEntityType);
        }
    }
}
