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
    using System;
    using System.Collections.ObjectModel;
    using System.Runtime.Serialization;
    using System.Spatial;

    /// <summary>
    /// Geography Multi-Polygon
    /// </summary>
    internal class GeographyMultiPolygonImplementation : GeographyMultiPolygon
    {
        /// <summary>
        /// Polygons
        /// </summary>
        private GeographyPolygon[] polygons;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="coordinateSystem">The CoordinateSystem</param>
        /// <param name="creator">The implementation that created this instance.</param>
        /// <param name="polygons">Polygons</param>
        internal GeographyMultiPolygonImplementation(CoordinateSystem coordinateSystem, SpatialImplementation creator, params GeographyPolygon[] polygons)
            : base(coordinateSystem, creator)
        {
            this.polygons = polygons;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="creator">The implementation that created this instance.</param>
        /// <param name="polygons">Polygons</param>
        internal GeographyMultiPolygonImplementation(SpatialImplementation creator, params GeographyPolygon[] polygons)
            : this(CoordinateSystem.DefaultGeography, creator, polygons)
        {
        }

        /// <summary>
        /// Is MultiPolygon Empty
        /// </summary>
        public override bool IsEmpty
        {
            get
            {
                return this.polygons.Length == 0;
            }
        }

        /// <summary>
        /// Geographies
        /// </summary>
        public override ReadOnlyCollection<Geography> Geographies
        {
            get { return this.polygons.AsReadOnly<Geography>(); }
        }

        /// <summary>
        /// Polygons
        /// </summary>
        public override ReadOnlyCollection<GeographyPolygon> Polygons
        {
            get { return this.polygons.AsReadOnly(); }
        }

        /// <summary>
        /// Sends the current spatial object to the given sink
        /// </summary>
        /// <param name="pipeline">The spatial pipeline</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "base does the validation")]
        public override void SendTo(GeographyPipeline pipeline)
        {
            base.SendTo(pipeline);
            pipeline.BeginGeography(SpatialType.MultiPolygon);
            for (int i = 0; i < this.polygons.Length; ++i)
            {
                this.polygons[i].SendTo(pipeline);
            }

            pipeline.EndGeography();
        }
    }
}
