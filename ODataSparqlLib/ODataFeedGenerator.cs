﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Query;

namespace ODataSparqlLib
{
    /// <summary>
    /// This class is responsible for converting a SPARQL results set into an OData feed
    /// </summary>
    public class ODataFeedGenerator
    {
        private readonly IODataRequestMessage _request;
        private readonly IODataResponseMessage _response;
        private readonly SparqlMap _map;
        private readonly string _baseUri;
        private readonly ODataMessageWriterSettings _writerSettings;

        /// <summary>
        /// Create a new feed generator
        /// </summary>
        /// <param name="requestMessage">The OData request message that was received</param>
        /// <param name="responseMessage">The OData response message to be populated by the generator</param>
        /// <param name="entityMap">The map to use to map RDF URIs to OData types and properties</param>
        /// <param name="baseUri">The base URI for the OData feed</param>
        /// <param name="messageWriterSettings">Additional settings to apply to the generated OData output</param>
        public ODataFeedGenerator(IODataRequestMessage requestMessage, IODataResponseMessage responseMessage, SparqlMap entityMap, string baseUri, ODataMessageWriterSettings messageWriterSettings)
        {
            _request = requestMessage;
            _response = responseMessage;
            _map = entityMap;
            _baseUri = baseUri;
            _writerSettings = messageWriterSettings;
        }

        /// <summary>
        /// Creates an OData feed response containing a list of entries for a particular type of entity
        /// </summary>
        /// <param name="resultsGraph">The RDF graph containing the SPARQL results</param>
        /// <param name="entityType">The fully qualified domain name for the type of entity to be written</param>
        /// <param name="resultsCount">The count of the total number of results that the server can provide</param>
        /// <param name="originalQueryModel">The SPARQL query that was executed to generate this graph</param>
        public void CreateFeedFromGraph(IGraph resultsGraph, string entityType, int resultsCount, SparqlModel originalQueryModel = null)
        {
            var msgWriter = new ODataMessageWriter(_response, _writerSettings, _map.Model);
            var feedWriter = msgWriter.CreateODataFeedWriter();
            var entries = new List<ODataEntry>();

            var typeUri = _map.GetUriForType(entityType);
            if (!String.IsNullOrEmpty(typeUri))
            {
                var predNode = resultsGraph.CreateUriNode(UriFactory.Create(RdfConstants.RdfType));
                var objNode = resultsGraph.CreateUriNode(UriFactory.Create(typeUri));
                if (originalQueryModel == null || originalQueryModel.Ordering == null)
                {
                    // No sorting required, just iterate all instances
                    foreach (var instanceTriple in resultsGraph.GetTriplesWithPredicateObject(predNode, objNode))
                    {
                        var instanceUri = (instanceTriple.Subject as IUriNode).Uri;
                        entries.Add(CreateODataEntry(resultsGraph, instanceUri.ToString(), entityType));
                    }
                }
                else
                {
                    // We need to apply the same sort criteria to this graph to ensure
                    // that the ODATA results are properly sorted.
                    // NOTE: This will only work if all the properties used in the original query
                    // are present in the graph - this could be a problem with more complex traversals
                    // and the query may instead need to be rewritten / regenerated to extract only
                    // the required sort properties.
                    originalQueryModel.IsDescribe = false;
                    var resultsTable =
                        resultsGraph.ExecuteQuery(originalQueryModel.GetSparqlRepresentation()) as SparqlResultSet;
                    var targetVariable= originalQueryModel.SelectVariables[0];
                    foreach (var result in resultsTable.Results)
                    {
                        var instanceUriNode = result[targetVariable] as IUriNode;
                        if (instanceUriNode != null)
                        {
                            entries.Add(CreateODataEntry(resultsGraph, instanceUriNode.Uri.ToString(), entityType));
                        }
                    }
                }
            }

            var feed = new ODataFeed {Id = _baseUri + _map.GetTypeSet(entityType)};
            if (_writerSettings.Version >= ODataVersion.V2)
            {
                feed.Count = resultsCount;
            }
            if (originalQueryModel != null)
            {
                feed.NextPageLink = GetNextPageLink(originalQueryModel);
            }
            feedWriter.WriteStart(feed);
            foreach (var entry in entries)
            {
                feedWriter.WriteStart(entry);
                feedWriter.WriteEnd();
            }
            feedWriter.WriteEnd();
            feedWriter.Flush();
        }

        private Uri GetNextPageLink(SparqlModel originalQueryModel)
        {
            if (!originalQueryModel.Limit.HasValue)
            {
                // No limit in the original query so there should be no next page
                return null;
            }
            if (!originalQueryModel.OriginalLimit.HasValue ||
                originalQueryModel.OriginalLimit.Value > originalQueryModel.Limit.Value)
            {
                // We need a next page link
                var thisPageLink = _request.Url.ToString();
                var queryParams = System.Web.HttpUtility.ParseQueryString(_request.Url.Query);
                int nextPageStart = originalQueryModel.Offset.HasValue
                                           ? originalQueryModel.Offset.Value + originalQueryModel.Limit.Value
                                           : originalQueryModel.Limit.Value;
                queryParams["$skip"] = nextPageStart.ToString(CultureInfo.InvariantCulture);
                string updatedQuery = "?" + String.Join("&",
                                                  queryParams.AllKeys.Select(
                                                      k => String.Format("{0}={1}", k, queryParams[k])));
                return new Uri(_request.Url, updatedQuery);
            }
            return null;
        }

        public void CreateFeedFromResultSet(SparqlResultSet resultSet)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an OData entry response message from an RDF graph
        /// </summary>
        /// <param name="resultsGraph">The SPARQL results graph to be processed</param>
        /// <param name="entryResource">The URI of the RDF resource to be converted to an entry</param>
        /// <param name="entryType">The fully qualified name of the type of entity that the RDF resource is to be converted to</param>
        public void CreateEntryFromGraph(IGraph resultsGraph, string entryResource, string entryType)
        {
            var msgWriter = new ODataMessageWriter(_response, _writerSettings, _map.Model);
            var entryWriter = msgWriter.CreateODataEntryWriter();
            var entry = CreateODataEntry(resultsGraph, entryResource, entryType);
            entryWriter.WriteStart(entry);
            var navigableProperties = WriteNavigationLinks(entryWriter, entry.ReadLink, resultsGraph, entryResource, entryType);
            if (_writerSettings.Version == null || _writerSettings.Version >= ODataVersion.V3)
            {
                entry.AssociationLinks = MakeAssociationLinks(entry.ReadLink, navigableProperties);
            }
            entryWriter.WriteEnd();
            entryWriter.Flush();
        }

        private ODataEntry CreateODataEntry(IGraph resultsGraph, string entryResource, string entryType)
        {
            var idPrefix = _map.GetResourceUriPrefix(entryType);
            if (!entryResource.StartsWith(idPrefix))
            {
                // Now we have a problem
                throw new Exception("Cannot create entry feed for resource " + entryResource +
                                    ". Resource URI does not start with the expected prefix " + idPrefix);
            }
            var resourceId = entryResource.Substring(idPrefix.Length);
            var odataLink = _baseUri + _map.GetTypeSet(entryType) + "('" + resourceId + "')";
            var entry = new ODataEntry
                {
                    TypeName = entryType,
                    ReadLink = new Uri(odataLink),
                    Id = odataLink
                };
            var subject = resultsGraph.CreateUriNode(UriFactory.Create(entryResource));
            var properties = new List<ODataProperty>();

            var identifierPropertyMapping = _map.GetIdentifierPropertyMapping(entryType);
            if (identifierPropertyMapping != null)
            {
                properties.Add(new ODataProperty{Name=identifierPropertyMapping.Name, Value=resourceId});
            }

            foreach (var propertyMapping in _map.GetStructuralPropertyMappings(entryType))
            {
                var predicate = resultsGraph.CreateUriNode(UriFactory.Create(propertyMapping.Uri));
                var match = resultsGraph.GetTriplesWithSubjectPredicate(subject, predicate).FirstOrDefault();
                if (match != null)
                {
                    if (match.Object is LiteralNode)
                    {
                        var newProperty = new ODataProperty
                            {
                                Name = propertyMapping.Name,
                                Value = GetValue(match.Object, propertyMapping.PropertyType)
                            };
                        properties.Add(newProperty);
                    }
                    else if (match.Object is UriNode && propertyMapping.PropertyType.IsPrimitive())
                    {
                        var newProperty = new ODataProperty()
                            {
                                Name = propertyMapping.Name,
                                Value = GetValue(match.Object, propertyMapping.PropertyType)
                            };
                        properties.Add(newProperty);
                    }
                }
            }

            

            if (_writerSettings.Version == null ||  _writerSettings.Version >= ODataVersion.V3)
            {
                var associationLinks = new List<ODataAssociationLink>();
                foreach (var assocMap in _map.GetAssociationPropertyMappings(entryType))
                {
                    var predicate = resultsGraph.CreateUriNode(UriFactory.Create(assocMap.Uri));
                    bool hasMatch = false;
                    if (assocMap.IsInverse)
                    {
                        hasMatch = resultsGraph.GetTriplesWithPredicateObject(predicate, subject).Any();
                    }
                    else
                    {
                        hasMatch = resultsGraph.GetTriplesWithSubjectPredicate(subject, predicate).Any();
                    }
                    // TODO: May need to be more specific here to catch inverse/forward versions of the same
                    // RDF property being mapped to two different OData properties (e.g. broader and narrower on a category)
                    // This quick hack will work for now though:
                    //bool hasMatch = resultsGraph.GetTriplesWithPredicate(resultsGraph.CreateUriNode(UriFactory.Create(assocMap.Uri))).Any();
                    if (hasMatch)
                    {
                        associationLinks.Add(new ODataAssociationLink
                            {
                                Name = assocMap.Name,
                                Url = new Uri(odataLink + "/$links/" + assocMap.Name)
                            });
                    }
                }
                entry.AssociationLinks = associationLinks;
            }

            entry.Properties = properties;
            return entry;
        }

        private IEnumerable<ODataAssociationLink> MakeAssociationLinks(Uri entryLink, IEnumerable<PropertyInfo> navigableProperties)
        {
            return navigableProperties.Select(p => new ODataAssociationLink
                {
                    Name=p.Name,
                    Url = new Uri(entryLink + "/$links/" + p.Name)
                });
        }

        private IEnumerable<PropertyInfo> WriteNavigationLinks(ODataWriter entryWriter, Uri entryLink, IGraph resultsGraph,
                                                        string entryResource, string entryType)
        {
            var subject = resultsGraph.CreateUriNode(UriFactory.Create(entryResource));
            var navigableProperties = new List<PropertyInfo>();
            foreach (var assocMap in _map.GetAssociationPropertyMappings(entryType))
            {
                var predicate = resultsGraph.CreateUriNode(UriFactory.Create(assocMap.Uri));
                List<Triple> matches = assocMap.IsInverse
                                           ? resultsGraph.GetTriplesWithPredicateObject(predicate, subject).ToList()
                                           : resultsGraph.GetTriplesWithSubjectPredicate(subject, predicate).ToList();
                if (matches.Any(t => t.Object is UriNode)) // Ignore literal and blank nodes as these cannot be entities
                {
                    var navLink = new ODataNavigationLink
                        {
                            IsCollection = assocMap.IsCollection,
                            Name = assocMap.Name,
                            Url = new Uri(entryLink + "/" + assocMap.Name)
                        };
                    entryWriter.WriteStart(navLink);
                    entryWriter.WriteEnd();
                    navigableProperties.Add(assocMap);
                }
            }
            return navigableProperties;
        }


        private static object GetValue(INode valueNode, IEdmTypeReference propertyType)
        {
            switch (propertyType.Definition.TypeKind)
            {
                case EdmTypeKind.Primitive:
                    try
                    {
                        return GetPrimitiveValue(valueNode.AsValuedNode(), propertyType.Definition as IEdmPrimitiveType,
                                                 propertyType.IsNullable);
                    }
                    catch (RdfException)
                    {
                        // Caught an error during typed value conversion so try a conversion on just the string value
                        if (valueNode is ILiteralNode)
                        {
                            var literalValue = (valueNode as ILiteralNode).Value;
                            return GetPrimitiveValue(literalValue, propertyType.Definition as IEdmPrimitiveType,
                                                     propertyType.IsNullable);
                        }
                        throw;
                    }
                default:
                    throw new NotImplementedException("No support for conversion to property type kind " +
                                                      propertyType.Definition.TypeKind);
            }
        }

        private static object GetPrimitiveValue(IValuedNode valuedNode, IEdmPrimitiveType targetType, bool asNullable)
        {
            // TODO: Some sort of cast to nullable when necessary
                switch (targetType.PrimitiveKind)
                {
                    case EdmPrimitiveTypeKind.Boolean:
                        return valuedNode.AsBoolean();
                    case EdmPrimitiveTypeKind.Byte:
                        return (byte) valuedNode.AsInteger();
                    case EdmPrimitiveTypeKind.DateTime:
                        return valuedNode.AsDateTime();
                    case EdmPrimitiveTypeKind.Decimal:
                        return valuedNode.AsDecimal();
                    case EdmPrimitiveTypeKind.Double:
                        return valuedNode.AsDouble();
                    case EdmPrimitiveTypeKind.Int16:
                        return (Int16) valuedNode.AsInteger();
                    case EdmPrimitiveTypeKind.Int32:
                        return (Int32) valuedNode.AsInteger();
                    case EdmPrimitiveTypeKind.Int64:
                        return valuedNode.AsInteger();
                    case EdmPrimitiveTypeKind.String:
                        return valuedNode.AsString();
                    case EdmPrimitiveTypeKind.DateTimeOffset:
                        return valuedNode.AsDateTime();
                    default:
                        throw new NotSupportedException(
                            String.Format("Support for primitive type {0} has not been implemented yet",
                                          targetType.PrimitiveKind));
                }
        }

        private static object GetPrimitiveValue(string value, IEdmPrimitiveType targetType, bool asNullable)
        {
            // TODO: Some sort of cast to nullable when necessary
            if (value == null) return NullOrDefault(targetType, asNullable);
            try
            {
                switch (targetType.PrimitiveKind)
                {
                    case EdmPrimitiveTypeKind.Boolean:
                        return Convert.ToBoolean(value);
                    case EdmPrimitiveTypeKind.Byte:
                        return Convert.ToByte(value);
                    case EdmPrimitiveTypeKind.DateTime:
                    case EdmPrimitiveTypeKind.DateTimeOffset:
                        return Convert.ToDateTime(value);
                    case EdmPrimitiveTypeKind.Decimal:
                        return Convert.ToDecimal(value);
                    case EdmPrimitiveTypeKind.Double:
                        return Convert.ToDouble(value);
                    case EdmPrimitiveTypeKind.Int16:
                        return Convert.ToInt16(value);
                    case EdmPrimitiveTypeKind.Int32:
                        return Convert.ToInt32(value);
                    case EdmPrimitiveTypeKind.Int64:
                        return Convert.ToInt64(value);
                    case EdmPrimitiveTypeKind.String:
                        return value;
                    default:
                        throw new NotSupportedException(
                            String.Format("Support for primitive type {0} has not been implemented yet",
                                          targetType.PrimitiveKind));
                }
            }
            catch (FormatException)
            {
                return NullOrDefault(targetType, asNullable);
            }
        }

        private static object NullOrDefault(IEdmPrimitiveType targetType, bool nullable)
        {
            switch (targetType.PrimitiveKind)
            {
                case EdmPrimitiveTypeKind.Boolean:
                    return nullable ? new bool?() : false;
                case EdmPrimitiveTypeKind.Byte:
                    return nullable ? new byte?() : 0;
                case EdmPrimitiveTypeKind.DateTime:
                case EdmPrimitiveTypeKind.DateTimeOffset:
                    return nullable ? new DateTime?() : DateTime.MinValue;
                case EdmPrimitiveTypeKind.Decimal:
                    return nullable ? new decimal?() : 0.0m;
                case EdmPrimitiveTypeKind.Double:
                    return nullable ? new double?() : double.NaN;
                case EdmPrimitiveTypeKind.Int16:
                    return nullable ? new short?() : 0;
                case EdmPrimitiveTypeKind.Int32:
                    return nullable ? new int?() : 0;
                case EdmPrimitiveTypeKind.Int64:
                    return nullable ? new long?() : 0;
                case EdmPrimitiveTypeKind.String:
                    return null;
                default:
                    throw new NotSupportedException(
                        String.Format("Support for primitive type {0} has not been implemented yet",
                                      targetType.PrimitiveKind));
            }
        }

        public void WriteServiceDocument()
        {
            var msgWriter = new ODataMessageWriter(_response, _writerSettings, _map.Model);
            var collections = new List<ODataResourceCollectionInfo>();
            foreach (
                var entityContainer in
                    _map.Model.EntityContainers().Where(ec => _map.Model.IsDefaultEntityContainer(ec)))
            {
                foreach (var es in entityContainer.EntitySets())
                {
                    var collectionInfo = new ODataResourceCollectionInfo {Url = new Uri(es.Name, UriKind.Relative)};
                    var metadata = new AtomResourceCollectionMetadata {Title = es.Name};
                    collectionInfo.SetAnnotation(metadata);
                    collections.Add(collectionInfo);
                }
            }
            var workspace = new ODataWorkspace {Collections = collections};
            msgWriter.WriteServiceDocument(workspace);
        }

        internal void CreateEntryFromGraphWithVariable(IGraph resultsGraph, string variableName, string entityType)
        {
            if (resultsGraph.IsEmpty) return;
            // Determine the resource that was bound to the specified SPARQL variable
            var resource = resultsGraph.GetTriplesWithPredicateObject(
                resultsGraph.CreateUriNode(new Uri("http://brightstardb.com/odata-sparql/variable-binding")),
                resultsGraph.CreateLiteralNode(variableName))
                        .Select(t => t.Subject)
                        .Cast<IUriNode>()
                        .Select(u => u.Uri)
                        .FirstOrDefault();
            if (resource == null) return;
            CreateEntryFromGraph(resultsGraph, resource.ToString(), entityType);
        }
    }
}
