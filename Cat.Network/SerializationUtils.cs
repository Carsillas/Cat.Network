using System;
using System.Buffers.Binary;
using System.Text;

namespace Cat.Network;
internal static class SerializationUtils {

	public static SerializationOptions UpdateOptions { get; } = new SerializationOptions {
		MemberIdentifierMode = MemberIdentifierMode.Index,
		MemberSelectionMode = MemberSelectionMode.Dirty,
		MemberSerializationMode = MemberSerializationMode.Partial
	};
	public static SerializationOptions CreateOptions { get; } = new SerializationOptions {
		MemberIdentifierMode = MemberIdentifierMode.Index,
		MemberSelectionMode = MemberSelectionMode.All,
		MemberSerializationMode = MemberSerializationMode.Complete
	};

	public static void ExtractPacketHeader(ReadOnlySpan<byte> buffer, out RequestType requestType, out Guid networkID, out Type type, out ReadOnlySpan<byte> contentBuffer) {
		contentBuffer = Span<byte>.Empty;
		ReadOnlySpan<byte> bufferCopy = buffer;

		requestType = (RequestType)bufferCopy[0];
		bufferCopy = bufferCopy.Slice(1);
		networkID = new Guid(bufferCopy.Slice(0, 16));
		bufferCopy = bufferCopy.Slice(16);
		type = null;

		if (requestType == RequestType.CreateEntity) {
			int typeNameLength = ReadTypeFullName(bufferCopy, out type);
			bufferCopy = bufferCopy.Slice(typeNameLength);
		}

		if (requestType == RequestType.CreateEntity || requestType == RequestType.UpdateEntity || requestType == RequestType.RPC) {
			int contentLength = BinaryPrimitives.ReadInt32LittleEndian(bufferCopy.Slice(0, 4));
			bufferCopy = bufferCopy.Slice(4);
			contentBuffer = bufferCopy.Slice(0, contentLength);
		}
	}

	public static int WritePacketHeader(Span<byte> buffer, RequestType requestType, NetworkEntity entity, out Span<byte> contentBuffer) {

		Span<byte> bufferCopy = buffer;

		bufferCopy[0] = (byte) requestType;
		bufferCopy = bufferCopy.Slice(1);
		entity.NetworkID.TryWriteBytes(bufferCopy.Slice(0, 16));
		bufferCopy = bufferCopy.Slice(16);
		
		if (requestType == RequestType.CreateEntity) {
			int typeNameLength = WriteTypeAssemblyQualifiedName(bufferCopy, entity);
			bufferCopy = bufferCopy.Slice(typeNameLength);
		}
		
		contentBuffer = bufferCopy;

		return buffer.Length - bufferCopy.Length;
	}

	private static int WriteTypeAssemblyQualifiedName(Span<byte> buffer, NetworkEntity entity) {
		Span<byte> stringBuffer = buffer.Slice(4);
		int stringByteLength = Encoding.Unicode.GetBytes(entity.GetType().AssemblyQualifiedName, stringBuffer);
		BinaryPrimitives.WriteInt32LittleEndian(buffer, stringByteLength);
		return stringByteLength + 4;
	}

	private static int ReadTypeFullName(ReadOnlySpan<byte> buffer, out Type type) {

		int typeNameLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		string typeName = Encoding.Unicode.GetString(buffer.Slice(4, typeNameLength));
		Type unverifiedType = Type.GetType(typeName);

		if (unverifiedType == null) {
			throw new Exception("Received Create Entity request with an unresolved type!");
		} else if (unverifiedType.IsSubclassOf(typeof(NetworkEntity))) {
			type = unverifiedType;
		} else {
			throw new Exception("Received Create Entity request with an invalid type!");
		}

		return typeNameLength + 4;
	}

}
