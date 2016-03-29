﻿using System;
using System.Runtime.InteropServices;

namespace ManagedBass.Enc
{
    public abstract class Encoder : IDisposable
    {
        protected int Channel { get; }

        public int Handle { get; protected set; }

        protected Encoder(int Channel) { this.Channel = Channel; }

        public bool AddChunk(string ID, IntPtr buffer, int length)
        {
            return BassEnc.EncodeAddChunk(Handle, ID, buffer, length);
        }

        public void EncodeAvailableFromDecoder()
        {
            if (!Bass.ChannelGetInfo(Channel).IsDecodingChannel)
                throw new InvalidOperationException("Not a Decoding Channel");

            var BlockLength = (int)Bass.ChannelSeconds2Bytes(Channel, 2);

            var Buffer = new byte[BlockLength];

            var gch = GCHandle.Alloc(Buffer, GCHandleType.Pinned);

            // while Decoder has Data
            while (Bass.ChannelIsActive(Channel) == PlaybackState.Playing)
                Bass.ChannelGetData(Channel, gch.AddrOfPinnedObject(), BlockLength);

            gch.Free();

            BassEnc.EncodeStop(Handle);
        }

        public bool IsActive => BassEnc.EncodeIsActive(Handle) == PlaybackState.Playing;

        public long QueueCount => BassEnc.EncodeGetCount(Handle, EncodeCount.Queue);
        public long InCount => BassEnc.EncodeGetCount(Handle, EncodeCount.In);
        public long OutCount => BassEnc.EncodeGetCount(Handle, EncodeCount.Out);
        public long QueueLimit => BassEnc.EncodeGetCount(Handle, EncodeCount.QueueLimit);
        public long CastCount => BassEnc.EncodeGetCount(Handle, EncodeCount.Cast);
        public long QueueFailCount => BassEnc.EncodeGetCount(Handle, EncodeCount.QueueFail);

        public void Dispose() => Bass.StreamFree(Channel);
    }

    public class ACMFileEncoder : Encoder
    {
        public ACMFileEncoder(string FileName, int Channel, EncodeFlags flags, WaveFormatTag encoding)
            : base(Channel)
        {
            // Get the Length of the ACMFormat structure
            var SuggestedFormatLength = BassEnc.GetACMFormat(0);
            var ACMFormat = Marshal.AllocHGlobal(SuggestedFormatLength);

            // Retrieve ACMFormat and Init Encoding
            if (BassEnc.GetACMFormat(Channel, ACMFormat, SuggestedFormatLength, null,
                                     // If encoding is Unknown, then let the User choose encoding.
                                     encoding == WaveFormatTag.Unknown ? 0 : ACMFormatFlags.Suggest,
                                     encoding) != 0)
                Handle = BassEnc.EncodeStartACM(Channel, ACMFormat, flags | EncodeFlags.AutoFree, FileName);

            else throw new BassException();

            // Free the ACMFormat structure
            Marshal.FreeHGlobal(ACMFormat);

            if (Handle == 0)
                throw new BassException();
        }
    }
}
