//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.IO;
using System.Linq;

namespace Iso7816
{
    /// <summary>
    /// Class ApduCommand implments the ISO 7816 apdu commands
    /// </summary>
    public class ApduCommand
    {
        public ApduCommand(byte cla, byte ins, byte p1, byte p2, byte[] commandData, byte? le)
        {
            if (commandData != null && commandData.Length > 254)
            {
                throw new NotImplementedException();
            }
            CLA = cla;
            INS = ins;
            P1 = p1;
            P2 = p2;
            CommandData = commandData;
            Le = le;

            ApduResponseType = typeof(Iso7816.ApduResponse);
        }
        /// <summary>
        /// Class of instructions
        /// </summary>
        public byte CLA { get; set; }
        /// <summary>
        /// Instruction code
        /// </summary>
        public byte INS { get; set; }
        /// <summary>
        /// Instruction parameter 1
        /// </summary>
        public byte P1 { get; set; }
        /// <summary>
        /// Instruction parameter 2
        /// </summary>
        public byte P2 { get; set; }
        /// <summary>
        /// Maximum number of bytes expected in the response ot this command
        /// </summary>
        public byte? Le { get; set; }
        /// <summary>
        /// Contiguous array of bytes representing commands data
        /// </summary>
        public byte[] CommandData { get; set; }
        /// <summary>
        /// Expected response type for this command.
        /// Provides mechanism to bind commands to responses
        /// </summary>
        public Type ApduResponseType { set; get; }
        /// <summary>
        /// Packs the current command into contiguous buffer bytes
        /// </summary>
        /// <returns>
        /// buffer holds the current wire/air format of the command
        /// </returns>
        public byte[] ToByteArray()
        {
            using (MemoryStream writer = new MemoryStream())
            {
                writer.WriteByte(CLA);
                writer.WriteByte(INS);
                writer.WriteByte(P1);
                writer.WriteByte(P2);

                if (CommandData != null && CommandData.Length > 0)
                {
                    writer.WriteByte((byte)CommandData.Length);
                    writer.Write(CommandData, 0, CommandData.Length);
                }

                if (Le != null)
                {
                    writer.WriteByte((byte)Le);
                }

                return writer.ToArray();
            }
        }
        /// <summary>
        /// Helper method to print the command in a readable format
        /// </summary>
        /// <returns>
        /// return string formatted command
        /// </returns>
        public override string ToString()
        {
            return "ApduCommand CLA=" + CLA.ToString("X2") + ",INS=" + INS.ToString("X2") + ",P1=" + P1.ToString("X2") + ",P2=" + P2.ToString("X2") + ((CommandData != null && CommandData.Length > 0) ? (",Data=" + BitConverter.ToString(CommandData).Replace("-", "")) : "");
        }
    }
    /// <summary>
    /// Class ApduResponse implments handler for the ISO 7816 apdu response
    /// </summary>
    public class ApduResponse
    {
        public const byte TAG_MULTI_BYTE_MASK = 0x1F;
        public const byte TAG_COMPREHENSION_MASK = 0x80;
        public const byte TAG_LENGTH_MULTI_BYTE_MASK = 0x80;

        /// <summary>
        /// Class constructor
        /// </summary>
        public ApduResponse() { }
        /// <summary>
        /// method to extract the response data, status and qualifier
        /// </summary>
        /// <param name="response"></param>
        public virtual void ExtractResponse(byte[] response)
        {
            if (response.Length < 2)
            {
                throw new InvalidOperationException("APDU response must be at least 2 bytes, got " + response.Length);
            }
            using (MemoryStream mem = new MemoryStream(response))
            using (BinaryReader reader = new BinaryReader(mem))
            {
                ResponseData = reader.ReadBytes(response.Length - 2);
                SW1 = reader.ReadByte();
                SW2 = reader.ReadByte();
            }
        }
        /// <summary>
        /// method to extract the matching TLV data object from response data
        /// </summary>
        public byte[] ExtractTlvDataObject(byte[] referenceTag)
        {
            using (MemoryStream mem = new MemoryStream(ResponseData))
            using (BinaryReader reader = new BinaryReader(mem))
            {
                byte nextByte;

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int lengthLength = 0, valueLength = 0;
                    MemoryStream tag = new MemoryStream(), value = new MemoryStream();

                    nextByte = reader.ReadByte();
                    tag.WriteByte(nextByte);

                    if ((nextByte & TAG_MULTI_BYTE_MASK) == TAG_MULTI_BYTE_MASK)
                    {
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            nextByte = reader.ReadByte();
                            tag.WriteByte(nextByte);

                            if ((nextByte & TAG_COMPREHENSION_MASK) != TAG_COMPREHENSION_MASK)
                                break;
                        }
                    }

                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        throw new Exception("Invalid length for TLV response");

                    valueLength = reader.ReadByte();
                    lengthLength = 1;

                    if ((valueLength & TAG_LENGTH_MULTI_BYTE_MASK) == TAG_LENGTH_MULTI_BYTE_MASK)
                        lengthLength += (valueLength & ~TAG_LENGTH_MULTI_BYTE_MASK);

                    while (--lengthLength > 0)
                        valueLength = (valueLength << 8) | reader.ReadByte();

                    while (valueLength != 0 && valueLength-- > 0)
                        value.WriteByte(reader.ReadByte());

                    if (referenceTag.SequenceEqual(tag.ToArray()))
                        return value.ToArray();
                }

                throw new Exception("Tag not found in the TLV response");
            }
        }
        /// <summary>
        /// Detects if the command has succeeded
        /// </summary>
        /// <returns></returns>
        public virtual bool Succeeded
        {
            get
            {
                return SW == 0x9000;
            }
        }
        /// <summary>
        /// command processing status
        /// </summary>
        public byte SW1 { get; set; }
        /// <summary>
        /// command processing qualifier
        /// </summary>
        public byte SW2 { get; set; }
        /// <summary>
        /// Wrapper property to read both response status and qualifer
        /// </summary>
        public ushort SW
        {
            get
            {
                return (ushort)(((ushort)SW1 << 8) | (ushort)SW2);
            }
            set
            {
                SW1 = (byte)(value >> 8);
                SW2 = (byte)(value & 0xFF);
            }
        }
        /// <summary>
        /// Response data
        /// </summary>
        public byte[] ResponseData { get; set; }
        /// <summary>
        /// Mapping response status and qualifer to human readable format
        /// </summary>
        public virtual string SWTranslation
        {
            get
            {
                switch (SW)
                {
                    case 0x9000:
                        return "Success";

                    case 0x6700:
                        return "Incorrect length or address range error";

                    case 0x6800:
                        return "The requested function is not supported by the card";

                    default:
                        return "Unknown";
                }
            }
        }
        /// <summary>
        /// Helper method to print the response in a readable format
        /// </summary>
        /// <returns>
        /// return string formatted response
        /// </returns>
        public override string ToString()
        {
            return "ApduResponse SW=" + SW.ToString("X4") + " (" + SWTranslation + ")" + ((ResponseData != null && ResponseData.Length > 0) ? (",Data=" + BitConverter.ToString(ResponseData).Replace("-", "")) : "");
        }
    }
    /// <summary>
    /// Class that implements select command
    /// </summary>
    public class SelectCommand : ApduCommand
    {
        public SelectCommand(byte[] aid, byte? le)
            : base((byte)Cla.CompliantCmd0x, (byte)Ins.SelectFile, 0x04, 0x00, aid, le)
        {
        }

        public byte[] AID
        {
            set { CommandData = value; }
            get { return CommandData; }
        }

        public override string ToString()
        {
            return "SelectCommand AID=" + BitConverter.ToString(CommandData).Replace("-", "");
        }
    }

    public class UpdateBinaryCommand : ApduCommand
    {
        public UpdateBinaryCommand(byte[] data, int address = 0)
            : base((byte)Cla.CompliantCmd0x, (byte)Ins.UpdateBinary, 0x00, 0x00, data, null)
        {
            if (address < 0 || address >= 1024)
                throw new InvalidOperationException("Address must not be larger than 1023");

            P1 = (byte)(address >> 8);
            P2 = (byte)(address & 0xFF);
        }

        public override string ToString()
        {
            return "UpdateBinaryCommand Address=" + ((P1 << 8) | P2) + " Data=" + BitConverter.ToString(CommandData).Replace("-", "");
        }
    }

    public class ReadBinaryCommand : ApduCommand
    {
        public ReadBinaryCommand(byte length = 0, int offset = 0)
            : base((byte)Cla.CompliantCmd0x, (byte)Ins.ReadBinary, 0x00, 0x00, null, length)
        {
            if (offset < 0 || offset >= 0x8000)
                throw new InvalidOperationException("Offset must not be larger than 32767");

            P1 = (byte)(offset >> 8);
            P2 = (byte)(offset & 0xFF);
        }

        public override string ToString()
        {
            return "ReadBinaryCommand Offset=" + ((P1 << 8) | P2) + " Length=" + Le;
        }
    }

    public class EraseBinaryCommand : ApduCommand
    {
        public EraseBinaryCommand(int offset = 0, int end = -1)
            : base((byte)Cla.CompliantCmd0x, (byte)Ins.EraseBinary, 0x00, 0x00, null, null)
        {
            if (offset < 0 || offset >= 0x8000)
                throw new InvalidOperationException("Offset must not be larger than 32767");
            if (end >= 0x8000)
                throw new InvalidOperationException("End must not be larger than 32767");

            P1 = (byte)(offset >> 8);
            P2 = (byte)(offset & 0xFF);

            if (end >= 0)
                CommandData = new byte[] { (byte)(end >> 8), (byte)(end & 0xFF) };
        }

        public override string ToString()
        {
            return "ReadBinaryCommand Offset=" + ((P1 << 8) | P2) + " Length=" + Le;
        }
    }
}
