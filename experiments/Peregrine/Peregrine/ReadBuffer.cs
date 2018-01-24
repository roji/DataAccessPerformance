﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Peregrine
{
    public class ReadBuffer
    {
        private const int DefaultBufferSize = 8192;

        private readonly AwaitableSocket _awaitableSocket;

        private readonly Memory<byte> _buffer = new Memory<byte>(new byte[DefaultBufferSize]);

        private int _position;

        internal ReadBuffer(AwaitableSocket awaitableSocket)
        {
            _awaitableSocket = awaitableSocket;
        }

        internal (MessageType Type, int Length) ReadMessage()
        {
            var messageType = (MessageType)ReadByte();
            var length = ReadInt() - 4;

            return (messageType, length);
        }

        internal string ReadErrorMessage()
        {
            string message = null;

            read:

            var code = (ErrorFieldTypeCode)ReadByte();

            switch (code)
            {
                case ErrorFieldTypeCode.Done:
                    break;
                case ErrorFieldTypeCode.Message:
                    message = ReadNullTerminatedString();
                    break;
                default:
                    ReadNullTerminatedString();
                    goto read;
            }

            return message;
        }

        public byte ReadByte()
            => _buffer.Span[_position];

        public byte[] ReadBytes(int length)
        {
            var bs = new byte[length];

            var span = _buffer.Span;
            for (var i = 0; i < length; i++)
                bs[i] = span[_position++];

            return bs;
        }

        public short ReadShort()
        {
            var result = BinaryPrimitives.ReadInt16BigEndian(_buffer.Span.Slice(_position, 2));
            _position += sizeof(short);
            return result;
        }

        public ushort ReadUShort()
        {
            var result = BinaryPrimitives.ReadUInt16BigEndian(_buffer.Span.Slice(_position, 2));
            _position += sizeof(short);
            return result;
        }

        public int ReadInt()
        {
            var result = BinaryPrimitives.ReadInt32BigEndian(_buffer.Span.Slice(_position, 4));
            _position += sizeof(int);
            return result;
        }

        public uint ReadUInt()
        {
            var result = BinaryPrimitives.ReadUInt32BigEndian(_buffer.Span.Slice(_position, 4));
            _position += sizeof(int);
            return result;
        }

        public string ReadNullTerminatedString()
        {
            var span = _buffer.Span;
            var length = _position;
            while (span[length++] != 0
                   && length < _buffer.Length)
            {
            }

            var result = PG.UTF8.GetString(span.Slice(_position, length));
            _position += length;
            return result;
        }

        public string ReadString(int length)
        {
            var result = PG.UTF8.GetString(_buffer.Span.Slice(_position, length));
            _position += length;
            return result;
        }

        public async Task ReceiveAsync()
        {
            _awaitableSocket.SetBuffer(_buffer);

            await _awaitableSocket.ReceiveAsync();

            var bytesTransferred = _awaitableSocket.BytesTransferred;

            if (bytesTransferred == 0)
            {
                throw new EndOfStreamException();
            }

            _position = 0;
        }
    }
}
