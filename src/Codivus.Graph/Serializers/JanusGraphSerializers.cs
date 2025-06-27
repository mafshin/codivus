using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Gremlin.Net.Structure.IO.GraphBinary;
using Gremlin.Net.Structure.IO.GraphBinary.Types;
using Gremlin.Net.Structure.IO.GraphSON;

namespace Codivus.Graph.Serializers
{
    /// <summary>
    /// Custom serializer for JanusGraph's RelationIdentifier type
    /// This handles the janusgraph.RelationIdentifier custom type that JanusGraph sends
    /// </summary>
    public class JanusGraphRelationIdentifierSerializer : CustomTypeSerializer
    {
        public override string TypeName => "janusgraph.RelationIdentifier";

        public override async Task WriteAsync(object value, Stream stream, GraphBinaryWriter writer, CancellationToken cancellationToken = default)
        {
            await WriteNullableValueAsync(value, stream, writer, cancellationToken);
        }

        public override async Task WriteNullableValueAsync(object? value, Stream stream, GraphBinaryWriter writer, CancellationToken cancellationToken = default)
        {
            if (value == null)
            {
                await writer.WriteAsync((string?)null, stream, cancellationToken);
                return;
            }

            await WriteNonNullableValueAsync(value, stream, writer, cancellationToken);
        }

        public override async Task WriteNonNullableValueAsync(object value, Stream stream, GraphBinaryWriter writer, CancellationToken cancellationToken = default)
        {
            // For writing, just treat it as a string
            await writer.WriteAsync(value.ToString() ?? "", stream, cancellationToken);
        }

        public override async Task<object?> ReadAsync(Stream stream, GraphBinaryReader reader, CancellationToken cancellationToken = default)
        {
            return await ReadNullableValueAsync(stream, reader, cancellationToken);
        }

        public override async Task<object?> ReadNullableValueAsync(Stream stream, GraphBinaryReader reader, CancellationToken cancellationToken = default)
        {
            try
            {
                // JanusGraph RelationIdentifier comes as a complex object
                // We need to consume the data from the stream without crashing
                // First read the actual payload
                var data = await reader.ReadAsync(stream, cancellationToken);
                
                // Return a simple placeholder - we don't actually need the RelationIdentifier data
                return new JanusGraphRelationIdentifier 
                { 
                    Value = data?.ToString() ?? Guid.NewGuid().ToString() 
                };
            }
            catch
            {
                // If we can't read it, return a placeholder
                return new JanusGraphRelationIdentifier { Value = Guid.NewGuid().ToString() };
            }
        }

        public override async Task<object> ReadNonNullableValueAsync(Stream stream, GraphBinaryReader reader, CancellationToken cancellationToken = default)
        {
            var result = await ReadNullableValueAsync(stream, reader, cancellationToken);
            return result ?? new JanusGraphRelationIdentifier { Value = Guid.NewGuid().ToString() };
        }
    }

    /// <summary>
    /// Placeholder class for JanusGraph RelationIdentifier
    /// </summary>
    public class JanusGraphRelationIdentifier
    {
        public string Value { get; set; } = "";
        
        public override string ToString() => Value;
        
        public override bool Equals(object? obj)
        {
            return obj is JanusGraphRelationIdentifier other && Value == other.Value;
        }
        
        public override int GetHashCode() => Value.GetHashCode();
    }

    /// <summary>
    /// Custom GraphSON deserializer for JanusGraph RelationIdentifier
    /// </summary>
    public class JanusGraphRelationIdentifierDeserializer : IGraphSONDeserializer
    {
        public dynamic Objectify(JsonElement graphsonObject, GraphSONReader reader)
        {
            // JanusGraph RelationIdentifier comes as a complex object
            // We just return a simple placeholder since we don't need the actual value
            return new JanusGraphRelationIdentifier { Value = Guid.NewGuid().ToString() };
        }
    }

    /// <summary>
    /// GraphSON3 message serializer factory that includes JanusGraph custom deserializers
    /// </summary>
    public static class JanusGraphGraphSON3MessageSerializerFactory
    {
        public static GraphSON3MessageSerializer Create()
        {
            var deserializers = new Dictionary<string, IGraphSONDeserializer>
            {
                { "janusgraph:RelationIdentifier", new JanusGraphRelationIdentifierDeserializer() }
            };
            
            // Create a GraphSON3Reader with our custom deserializers
            var reader = new GraphSON3Reader(deserializers);
            
            // Create a custom GraphSON3MessageSerializer with the reader
            return new GraphSON3MessageSerializer(reader);
        }
    }

    /// <summary>
    /// Type serializer registry builder that includes JanusGraph custom types
    /// </summary>
    public static class JanusGraphTypeSerializerRegistry
    {
        public static TypeSerializerRegistry Create()
        {
            // Use reflection to add the custom serializer by type name
            var builder = TypeSerializerRegistry.Build();
            var serializer = new JanusGraphRelationIdentifierSerializer();
            
            // Try to register the custom type by typename using reflection
            try
            {
                var builderType = builder.GetType();
                var customTypesField = builderType.GetField("_customTypesToRegister", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (customTypesField != null)
                {
                    var customTypes = customTypesField.GetValue(builder) as System.Collections.Generic.IDictionary<string, object>;
                    if (customTypes != null)
                    {
                        customTypes[serializer.TypeName] = serializer;
                    }
                }
            }
            catch
            {
                // Fallback to normal registration
            }
            
            // Also try the normal way
            builder.AddCustomType(typeof(JanusGraphRelationIdentifier), serializer);
            
            return builder.Create();
        }
    }
}