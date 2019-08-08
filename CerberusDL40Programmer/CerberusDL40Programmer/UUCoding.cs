using System;
using System.Text;

namespace NXPFlashLoader
{
  public static class UUCoding
  {
    private static byte[] extraBytes = new byte[4];

    // Buffers are pre-allocated!
    // ASCII buffer must be at least allocated to a size equal or grater than (4 / 3 * binBufferSize)
    // Returns the ASCII buffer size
    public static int BinaryToASCII(byte[] binaryBuffer, int binBufferOffset, byte[] asciiBuffer, int asciiBufferOffset, int binBufferSize)
    {
      // 3 bytes increment for binaryIndex, 4 to ascii_index
      int asciiIndex, binaryIndex;

      for (binaryIndex = 0, asciiIndex = 0; binaryIndex < binBufferSize; binaryIndex += 3, asciiIndex += 4)
      {
        // first ascii
        asciiBuffer[asciiBufferOffset + asciiIndex] = (byte)((binaryBuffer[binBufferOffset + binaryIndex] >> 2) + 32);	// Get the first 6 bits
        if (asciiBuffer[asciiBufferOffset + asciiIndex] == 0x20) // turn space into '\'
          asciiBuffer[asciiBufferOffset + asciiIndex] = 0x60;

        // second ascii
        if (binaryIndex + 1 != binBufferSize)		// end of binaryBuffer
        {
          asciiBuffer[asciiBufferOffset + asciiIndex + 1] = (byte)((((binaryBuffer[binBufferOffset + binaryIndex] & 0x3) << 4) | ((binaryBuffer[binBufferOffset + binaryIndex + 1]) >> 4)) + 32); // Get the last 2 and first four of the second one
          if (asciiBuffer[asciiBufferOffset + asciiIndex + 1] == 0x20)
            asciiBuffer[asciiBufferOffset + asciiIndex + 1] = 0x60;
        }
        else
        {
          asciiBuffer[asciiBufferOffset + asciiIndex + 1] = (byte)(((binaryBuffer[binBufferOffset + binaryIndex] & 0x3) << 4) + 32);
          if (asciiBuffer[asciiBufferOffset + asciiIndex + 1] == 0x20)
            asciiBuffer[asciiBufferOffset + asciiIndex + 1] = 0x60;
          return asciiIndex + 2;
        }

        // third ascii
        if (binaryIndex + 2 != binBufferSize)		// end of binaryBuffer
        {
          asciiBuffer[asciiBufferOffset + asciiIndex + 2] = (byte)((((binaryBuffer[binBufferOffset + binaryIndex + 1] & 0x0F) << 2) | ((binaryBuffer[binBufferOffset + binaryIndex + 2]) >> 6)) + 32); // Get the last 4 and first 2 of the second one
          if (asciiBuffer[asciiBufferOffset + asciiIndex + 2] == 0x20)
            asciiBuffer[asciiBufferOffset + asciiIndex + 2] = 0x60;
        }
        else
        {
          asciiBuffer[asciiBufferOffset + asciiIndex + 2] = (byte)(((binaryBuffer[binBufferOffset + binaryIndex + 1] & 0x0F) << 2) + 32);
          if (asciiBuffer[asciiBufferOffset + asciiIndex + 2] == 0x20)
            asciiBuffer[asciiBufferOffset + asciiIndex + 2] = 0x60;
          return asciiIndex + 3;
        }

        // forth ascii
        asciiBuffer[asciiBufferOffset + asciiIndex + 3] = (byte)((binaryBuffer[binBufferOffset + binaryIndex + 2] & 0x3F) + 32); // Get the last 6
        if (asciiBuffer[asciiBufferOffset + asciiIndex + 3] == 0x20)
          asciiBuffer[asciiBufferOffset + asciiIndex + 3] = 0x60;
      }

      return asciiIndex;
    }


    // changes ASCII BUFFERSSSSSSSSSSSSSSSSSSSSSSSSSSSSS
    public static int ASCIIToBinary(byte[] binaryBuffer, int binBufferOffset, byte[] asciiBuffer, int asciiBufferOffset, int asciiBufferCount)
    {
      int binaryIndex, asciiIndex;

      // first pass turn 0x60 back into 0 and everything else minus 0x20
      for (int i = 0; i < asciiBufferCount; i++)
        if (asciiBuffer[asciiBufferOffset + i] == 0x60)
          asciiBuffer[asciiBufferOffset + i] = 0;
        else
          asciiBuffer[asciiBufferOffset + i] -= 0x20;

      for (binaryIndex = 0, asciiIndex = 0; (asciiIndex + 4) <= asciiBufferCount; binaryIndex += 3, asciiIndex += 4)
      {
        binaryBuffer[binBufferOffset + binaryIndex] = (byte)((asciiBuffer[asciiBufferOffset + asciiIndex] << 2) | (asciiBuffer[asciiBufferOffset + asciiIndex + 1] >> 4));
        binaryBuffer[binBufferOffset + binaryIndex + 1] = (byte)((asciiBuffer[asciiBufferOffset + asciiIndex + 1] << 4) | (asciiBuffer[asciiBufferOffset + asciiIndex + 2] >> 2));
        binaryBuffer[binBufferOffset + binaryIndex + 2] = (byte)((asciiBuffer[asciiBufferOffset + asciiIndex + 2] << 6) | (asciiBuffer[asciiBufferOffset + asciiIndex + 3]));
      }

      // last bytes
      if (asciiIndex != asciiBufferCount)
      {
        extraBytes[0] = extraBytes[1] = extraBytes[2] = extraBytes[3] = 0;
        Array.Copy(asciiBuffer, asciiBufferOffset + asciiIndex, extraBytes, 0, asciiBufferCount - asciiIndex);
        binaryBuffer[binBufferOffset + binaryIndex] = (byte)((extraBytes[0] << 2) | (extraBytes[0 + 1] >> 4));
        binaryBuffer[binBufferOffset + binaryIndex + 1] = (byte)((extraBytes[0 + 1] << 4) | (extraBytes[0 + 2] >> 2));
        binaryBuffer[binBufferOffset + binaryIndex + 2] = (byte)((extraBytes[0 + 2] << 4) | (extraBytes[0 + 3]));

        binaryIndex += asciiBufferCount - asciiIndex;
      }


      return binaryIndex;
    }
  }
}
