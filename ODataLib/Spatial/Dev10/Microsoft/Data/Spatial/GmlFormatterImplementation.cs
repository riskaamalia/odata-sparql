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

namespace Microsoft.Data.Spatial
{
    using System.Spatial;
    using System.Xml;
    using System.IO;

    /// <summary>
    /// The object to move spatial types to and from the GML format
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Gml", Justification = "Gml is the accepted name in the industry")]
    internal class GmlFormatterImplementation : GmlFormatter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GmlFormatterImplementation"/> class.
        /// </summary>
        /// <param name="creator">The implementation that created this instance.</param>
        internal GmlFormatterImplementation(SpatialImplementation creator)
            : base(creator)
        {
        }

        /// <summary>
        /// Create the writer
        /// </summary>
        /// <param name="target">The object that should be the target of the ISpatialPipeline writer.</param>
        /// <returns>A writer that implements ISpatialPipeline.</returns>
        public override SpatialPipeline CreateWriter(XmlWriter target)
        {
            return new ForwardingSegment(new GmlWriter(target));
        }

        /// <summary>
        /// Reads the geography.
        /// </summary>
        /// <param name="readerStream">The reader stream.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected override void ReadGeography(XmlReader readerStream, SpatialPipeline pipeline)
        {
            new GmlReader(pipeline).ReadGeography(readerStream);
        }

        /// <summary>
        /// Reads the geometry.
        /// </summary>
        /// <param name="readerStream">The reader stream.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected override void ReadGeometry(XmlReader readerStream, SpatialPipeline pipeline)
        {
            new GmlReader(pipeline).ReadGeometry(readerStream);
        }
    }
}
