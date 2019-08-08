using System;
using System.Text;
using Microsoft.SPOT;

namespace NXPFlashLoader
{
  public interface CommunicationInterface
  {
    void Initialize();
    void Read(byte[] buffer, int offset, int size, int timeout);
    void Write(byte[] buffer, int offset, int size);
    void Purge();
  }

  static class NXP
  {
    // it is represented in KHz
    public enum Frequency : uint
    {
      MHz_10 = 10000,
      MHz_12 = 12000,
      MHZ_14_746 = 14746,
      MHz_72 = 72000,
      MHz_168 = 168000,
    }
    public class ChipDef
    {
      public ChipDef(string partName, int sectorCount)
      {
        this.partName = partName;
        this.sectorCount = sectorCount;
      }

      public string partName;
      public int sectorCount;
    }

    public static class Chips
    {
      public static ChipDef LPC2103 = new ChipDef("LPC2103", 8);
      public static ChipDef LPC2134 = new ChipDef("LPC2134", 11);
      public static ChipDef LPC2468 = new ChipDef("LPC2468", 28);
      public static ChipDef LPC2478 = new ChipDef("LPC2478", 28);
      public static ChipDef LPC1111 = new ChipDef("LPC1111", 6);
    }

    public class Programmer
    {
      const int TIMEOUT = 3000;

      private CommunicationInterface com;
      private ChipDef chipDef;
      private uint oscFreq;
      private byte[] cmdBuffer = new byte[128];  // command buffer
      private byte[] lastSectorBuffer = new byte[PROGRAMMBLE_SIZE + 4]; // copy to extra sector, if not muiltple of  PROGRAMMBLE_SIZE
      //private byte[] uuBuffer = new byte[PROGRAMMBLE_SIZE * 4 / 3 + 4];   // For uuCoding
      private byte[] m_uuBuffer;   // For uuCoding
      private byte[] m_VerifyUUBuffer;
      private byte[] m_binBuffer;   // For uuCoding
      //private byte[] compareSectorBuffer = new byte[PROGRAMMBLE_SIZE + 4]; // copy to extra sector, if not muiltple of  PROGRAMMBLE_SIZE

      // to speed up stuff
      string WRITE_TO_RAM_STRING = "W " + FIRST_PROGRAMMABLE_RAM_ADDRESS.ToString() + " " + PROGRAMMBLE_SIZE.ToString() + "\r\n";
      byte[] WRITE_TO_RAM_STRING_BYTES, PREPARE_SECTORS_BYTES;
      byte[] OK_STRING_BYTES = Encoding.UTF8.GetBytes("OK\r\n");

      const uint FIRST_PROGRAMMABLE_RAM_ADDRESS = 0x10000200;

      const int CMD_SUCCESS = 0;
      const int MAXIMUM_LINE_BYTES = 45;
      const int MAXIMUM_LINE_ASCII = 45 * 4 / 3;
      const int MAXIMUM_LINES_COUNT = 20;
      const int PROGRAMMBLE_SIZE = 512;
      private const int UU_XFER_SIZE = 683;
      const byte EMPTY_BYTE = 0xFF;
      int currentUUIndex = 0;
      string COPY_TO_RAM_STRING;
      string VERIFY_STRING;

      private void DelayMS(int ms)
      {
        System.Threading.Thread.Sleep(ms);
      }

      private uint GetCheckSum(byte[] buffer, int offset, int count)
      {
        uint result = 0;

        for (int i = 0; i < count; i++)
        {
          result += buffer[offset + i];
        }

        return result;
      }

      private bool Compare(byte[] buffer1, int buffer1Offset, byte[] buffer2, int buffer2Offset, int count)
      {
        for (int i = 0; i < count; i++)
          if (buffer1[buffer1Offset + i] != buffer2[buffer2Offset + i])
            return false;

        return true;
      }

      public Programmer(CommunicationInterface com, ChipDef chip, Frequency OscFreq)
      {
        this.com = com;
        this.chipDef = chip;
        this.oscFreq = (uint)OscFreq;

        WRITE_TO_RAM_STRING_BYTES = Encoding.UTF8.GetBytes(WRITE_TO_RAM_STRING);
        PREPARE_SECTORS_BYTES = Encoding.UTF8.GetBytes("P 0 " + (chipDef.sectorCount - 1) + "\r\n");
        COPY_TO_RAM_STRING = " " + FIRST_PROGRAMMABLE_RAM_ADDRESS.ToString() + " " + PROGRAMMBLE_SIZE.ToString() + "\r\n";
        VERIFY_STRING = " " + PROGRAMMBLE_SIZE.ToString() + "\r\n";
      }

      public void SelectCom(CommunicationInterface com)
      {
        this.com = com;
      }

      public void Connect()
      {
        byte[] dummyBuffer;

        com.Initialize();
        com.Purge();

        // send "?"
        com.Write(Encoding.UTF8.GetBytes("?"), 0, 1);

        // Read "Synchronized"
        long initialTime = DateTime.Now.Ticks / 10000;
        do
        {
          com.Read(cmdBuffer, 0, 1, TIMEOUT);
          if (cmdBuffer[0] == (byte)'S')
            break;
        } while (((DateTime.Now.Ticks / 10000) - initialTime) <= TIMEOUT);
        // Read "Synchronized"		// 12 chars + \r + \n = 14
        com.Read(cmdBuffer, 1, 13, TIMEOUT);
        if (Compare(cmdBuffer, 0, Encoding.UTF8.GetBytes("Synchronized\r\n"), 0, 14) == false)
          throw new Exception("Syncronized not received");

        // Resend "Synchronized"
        com.Write(cmdBuffer, 0, 14);

        // Read "Synchronized\r\nOK\r\n"		// ECHOed
        com.Read(cmdBuffer, 0, 18, TIMEOUT);
        if (Compare(cmdBuffer, 0, Encoding.UTF8.GetBytes("Synchronized\r\nOK\r\n"), 0, 18) == false)
          throw new Exception("Syncronized not echoed");

        // Send Freq.
        dummyBuffer = Encoding.UTF8.GetBytes(oscFreq.ToString());
        com.Write(dummyBuffer, 0, dummyBuffer.Length);
        cmdBuffer[0] = (byte)'\r';
        cmdBuffer[1] = (byte)'\n';
        com.Write(cmdBuffer, 0, 2);

        // Read Freq.		// ECHOed
        com.Read(cmdBuffer, 0, dummyBuffer.Length, TIMEOUT);
        if (Compare(cmdBuffer, 0, dummyBuffer, 0, dummyBuffer.Length) == false)
          throw new Exception("Freq error");
        com.Read(cmdBuffer, 0, 6, TIMEOUT); // see below
        if (Compare(cmdBuffer, 0, Encoding.UTF8.GetBytes("\r\nOK\r\n"), 0, 6) == false)
          throw new Exception("Freq error");

        // Turn Echo off
        dummyBuffer = Encoding.UTF8.GetBytes("A 0\r\n");
        com.Write(dummyBuffer, 0, dummyBuffer.Length);
        // Read Echo
        com.Read(cmdBuffer, 0, dummyBuffer.Length, TIMEOUT);
        if (Compare(cmdBuffer, 0, dummyBuffer, 0, dummyBuffer.Length) == false)
          throw new Exception("Echo error");
        // Verify Response
        if (GetCMDResponse() != CMD_SUCCESS)
          throw new Exception("Echo error");

        // unlock flash Write, Erase, and Go commands
        SendCommand("U 23130\r\n");

      }

      private void SendCommand(string cmd)
      {
        byte[] dummyBuffer;
        // send command
        dummyBuffer = Encoding.UTF8.GetBytes(cmd);
        com.Write(dummyBuffer, 0, dummyBuffer.Length);

        // verify response
        int response = GetCMDResponse();
        if (response != CMD_SUCCESS)
          throw new Exception("Error response: " + response.ToString());
      }

      private void SendCommand(byte[] bytes)
      {
        //   byte[] dummyBuffer;
        // send command
        // dummyBuffer = Encoding.UTF8.GetBytes(cmd);
        com.Write(bytes, 0, bytes.Length);

        // verify response
        int response = GetCMDResponse();
        if (response != CMD_SUCCESS)
          throw new Exception("Error response: " + response.ToString());
      }

      private int GetCMDResponse()
      {
        // digits
        com.Read(cmdBuffer, 0, 3, TIMEOUT);
        if (cmdBuffer[1] != '\r')
        {
          com.Read(cmdBuffer, 3, 1, TIMEOUT);
          if (cmdBuffer[3] != '\n')
            throw new Exception("Invalid Response");

          return (cmdBuffer[0] - 0x30) * 10 + (cmdBuffer[1] - 0x30);
        }
        else
        {
          if (cmdBuffer[2] != '\n')
            throw new Exception("Invalid Response");

          return cmdBuffer[0] - 0x30;
        }
      }

      public void SelectDownloadBuffer(byte[] buffer, int offset, int count)
      {
        if (count < 512)
          throw new Exception("Buffer too small");

        m_binBuffer = new byte[count];
        Array.Copy(buffer, offset, m_binBuffer, 0, count);
        buffer = m_binBuffer;

        m_uuBuffer = new byte[(count + 512) * 4 / 3 + 4];
        m_VerifyUUBuffer = new byte[m_uuBuffer.Length];

        int index = 512;
        int uuOffset = UU_XFER_SIZE;

        //////////////////////////////////////////////////////////////////////////
        // checksum and 2's complement here

        uint checksum = 0;
        uint num = 0;

        for (int i = 0; i < 39; i += 4)
        {
          num = 0;

          num |= (uint)(buffer[i + 3] << 24);
          num |= (uint)(buffer[i + 2] << 16);
          num |= (uint)(buffer[i + 1] << 8);
          num |= (uint)(buffer[i + 0] << 0);

          checksum += num;// m_uuBuffer[i];
        }


        checksum = ~checksum;

        checksum++;

        uint first = 0;
        uint second = 0;
        uint third = 0;
        uint fourth = 0;

        first = (checksum & 0xFF);
        first = first >> 0;

        second = (checksum & 0xFF00);
        second = second >> 8;

        third = (checksum & 0xFF0000);
        third = third >> 16;

        fourth = (checksum & 0xFF000000);
        fourth = fourth >> 24;

        checksum = (first << 24) | (second << 16) | (third << 8) | (fourth << 0);

        buffer[28] = (byte)(first);
        buffer[29] = (byte)(second);
        buffer[30] = (byte)(third);
        buffer[31] = (byte)(fourth);
        //////////////////////////////////////////////////////////////////////////

        // do uuBuffer
        while (index < count)
        {
          Debug.GC(true);
          // is there enough data for a sector?
          if ((index + PROGRAMMBLE_SIZE) > count)
          {
            Array.Copy(buffer, index, lastSectorBuffer, 0, count - index);
            for (int i = count - index; i < PROGRAMMBLE_SIZE; i++)
              lastSectorBuffer[i] = EMPTY_BYTE;
            UUCoding.BinaryToASCII(lastSectorBuffer, 0, m_uuBuffer, uuOffset, PROGRAMMBLE_SIZE);
          }
          else
          {
            UUCoding.BinaryToASCII(buffer, index, m_uuBuffer, uuOffset, PROGRAMMBLE_SIZE);
          }

          index += PROGRAMMBLE_SIZE;
          uuOffset += UU_XFER_SIZE;
        }

        UUCoding.BinaryToASCII(buffer, 0, m_uuBuffer, 0, PROGRAMMBLE_SIZE);
      }

      public void EraseAll()
      {
        // Prepare all sectors
        SendCommand("P 0 " + (chipDef.sectorCount - 1) + "\r\n");

        // Erase all sectors
        SendCommand("E 0 " + (chipDef.sectorCount - 1) + "\r\n");
      }

      public void Download()
      {
        int index = 512; // skip protection 

        // write
        currentUUIndex = UU_XFER_SIZE;
        while (index < m_binBuffer.Length)
        {
          DelayMS(1);
          // Prepare all sectors
          SendCommand(PREPARE_SECTORS_BYTES);

          // is there enough data for a sector?
          if ((index + PROGRAMMBLE_SIZE) > m_binBuffer.Length)
          {
            Array.Copy(m_binBuffer, index, lastSectorBuffer, 0, m_binBuffer.Length - index);
            for (int i = m_binBuffer.Length - index; i < PROGRAMMBLE_SIZE; i++)
              lastSectorBuffer[i] = EMPTY_BYTE;

            DownloadToFlash(index, lastSectorBuffer, 0, PROGRAMMBLE_SIZE);
          }
          else
          {
            DownloadToFlash(index, m_binBuffer, index, PROGRAMMBLE_SIZE);
          }

          index += PROGRAMMBLE_SIZE;
          currentUUIndex += UU_XFER_SIZE;
        }

        currentUUIndex = 0;
        // download first sector
        DownloadToFlash(0, m_binBuffer, 0, 512);
      }

      private void DownloadToFlash(int address, byte[] buffer, int offset, int count)
      {
        byte[] dummyBuffer;
        int index = 0;
        if (count != 512) throw new Exception("Size Not supported yet");
        if (address < 64 && address != 0) throw new Exception("address Not supported yet");

        // Write to RAM
        SendCommand(WRITE_TO_RAM_STRING_BYTES);
        // send data // pre-calculated values for now
        cmdBuffer[0] = (byte)'\r';
        cmdBuffer[1] = (byte)'\n';
        cmdBuffer[2] = 45 + 32; // number of bytes in a line uuCoded
        for (int i = 0; i < 11; i++)
        {
          com.Write(cmdBuffer, 2, 1);   // number of bytes in a line
          com.Write(m_uuBuffer, currentUUIndex + index, 60); // bytes
          com.Write(cmdBuffer, 0, 2);   // <CR>
          index += 60;
          DelayMS(1);
        }
        // last 17 bytes (23 uuCoding)
        cmdBuffer[2] = 17 + 32;
        com.Write(cmdBuffer, 2, 1);   // number of bytes in a line
        com.Write(m_uuBuffer, currentUUIndex + index, 23);
        com.Write(cmdBuffer, 0, 2); // <CR>
        // send checksum
        dummyBuffer = Encoding.UTF8.GetBytes(GetCheckSum(buffer, (int)offset, PROGRAMMBLE_SIZE) + "\r\n");
        com.Write(dummyBuffer, 0, dummyBuffer.Length);
        // Read OK
        com.Read(cmdBuffer, 0, 4, TIMEOUT);
        if (Compare(cmdBuffer, 0, OK_STRING_BYTES, 0, 4) == false)
        {
          com.Purge();
          throw new Exception("Checksum Failed");
        }

        // Copy Ram to Flash
        SendCommand("C " + address.ToString() + COPY_TO_RAM_STRING);
      }

      public void Verify()
      {
        DelayMS(400);

        // verify
        currentUUIndex = UU_XFER_SIZE;
        int index = 512;
        while (index < m_binBuffer.Length)
        {
          DelayMS(1);

          // is there enough data for a sector?
          if ((index + PROGRAMMBLE_SIZE) > m_binBuffer.Length)
          {
            Array.Copy(m_binBuffer, index, lastSectorBuffer, 0, m_binBuffer.Length - index);
            for (int i = m_binBuffer.Length - index; i < PROGRAMMBLE_SIZE; i++)
              lastSectorBuffer[i] = EMPTY_BYTE;

            VerifyFlash(index, lastSectorBuffer, 0, PROGRAMMBLE_SIZE);
          }
          else
          {
            VerifyFlash(index, m_binBuffer, index, PROGRAMMBLE_SIZE);
          }

          index += PROGRAMMBLE_SIZE;
          currentUUIndex += UU_XFER_SIZE;
        }

        currentUUIndex = 0;
        index = 0;
        VerifyFlash(0, m_binBuffer, 0, PROGRAMMBLE_SIZE);
      }

      private void VerifyFlash(int address, byte[] buffer, int offset, int count)
      {

        int index = 0;
        int t1, t2;
        if (count != 512) throw new Exception("Size Not supported yet");
        if (address < 64 && address != 0) throw new Exception("address Not supported yet");

        // read back and verify
        SendCommand("R " + address.ToString() + VERIFY_STRING);

        for (int i = 0; i < 11; i++)
        {
          com.Read(cmdBuffer, 1, 1, TIMEOUT);   // number of bytes in a line
          com.Read(m_VerifyUUBuffer, currentUUIndex + index, 60, TIMEOUT); // bytes
          com.Read(cmdBuffer, 0, 2, TIMEOUT);   // <CR><LF>
          index += 60;
        }
        // last 17 bytes (23 uuCoding)
        com.Read(cmdBuffer, 1, 1, TIMEOUT);   // number of bytes in a line
        com.Read(m_VerifyUUBuffer, currentUUIndex + index, 23, TIMEOUT);
        com.Read(cmdBuffer, 0, 3, TIMEOUT); // <CR>
        // read checksum
        while (true)
        {
          com.Read(cmdBuffer, 0, 1, TIMEOUT);
          if (cmdBuffer[0] == (byte)'\n')
            break;
        }
        // send OK
        com.Write(OK_STRING_BYTES, 0, 4);

        // skip first 64 bytes verification (86 uu-coded, maybe..)
        if (address < 64)
        {
          if (Compare(m_uuBuffer, currentUUIndex + 86, m_VerifyUUBuffer, currentUUIndex + 86, UU_XFER_SIZE - 86 - 1) == false)
            throw new Exception("Verification Failed sector 0...");
        }
        else
        {
          if (Compare(m_uuBuffer, currentUUIndex, m_VerifyUUBuffer, currentUUIndex, UU_XFER_SIZE - 1) == false)
            throw new Exception("Verification Failed...");
        }

        // compare last uuCode/byte
        t1 = m_uuBuffer[currentUUIndex + UU_XFER_SIZE - 1];
        if (t1 == 0x60)
          t1 = 0;
        else
          t1 -= 0x20;

        t2 = m_VerifyUUBuffer[currentUUIndex + UU_XFER_SIZE - 1];
        if (t2 == 0x60)
          t2 = 0;
        else
          t2 -= 0x20;

        if ((t1 & 0xFC) != (t2 & 0xFC))
          throw new Exception("Verification Failed 2...");

      }
    }
  }
}
