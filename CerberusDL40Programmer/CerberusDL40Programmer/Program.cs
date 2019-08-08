// License Apache 2.0
// This is the CerberusDL40Programmer by tylorza
// adapted to NETMF 4.3 by RoSchmi

using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Touch;
using System.Text;

using NXPFlashLoader;
using System.IO.Ports;
using Microsoft.SPOT.Hardware;

namespace CerberusDL40Programmer
{
  public class SerialPortToNXP : NXPFlashLoader.CommunicationInterface
  {
    public SerialPort serial;
    public SerialPortToNXP(SerialPort port)
    {
      serial = port;
    }

    public void Initialize()
    {
      //not used
    }

    public void Read(byte[] buffer, int offset, int size, int timeout)
    {
      DateTime startTime = DateTime.Now;
      do
      {
        int bytesRead = serial.Read(buffer, offset, size);
        offset += bytesRead;
        size -= bytesRead;

        if ((DateTime.Now - startTime).Milliseconds > timeout)
          throw new Exception("Timeout");
      } while (size > 0);

      if (size != 0) throw new Exception("Data size mismatch");
    }

    public void Write(byte[] buffer, int offset, int size)
    {
      serial.Write(buffer, offset, size);
    }

    public void Purge()
    {
      serial.DiscardInBuffer();
    }
  }

  public partial class Program
  {

    // Change me to the .bin file that you wish to flash to the module.
    public static byte[] binFile = Resources.GetBytes(Resources.BinaryResources.DL40);

    public static Thread flashThread;

    public static InterruptPort button;
    public static OutputPort reset;
    public static OutputPort loader;

    public static SerialPort serial;

    public static bool doneFlash = true;

    // This method is run when the mainboard is powered up or reset.   
    public static void Main()
    {      
      // Port 7 Pin 3
      button = new InterruptPort(Cpu.Pin.GPIO_Pin15, true, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeLow);
      button.OnInterrupt += new NativeEventHandler(button_OnInterrupt);
      
      // set up pins to reset board into bootloader mode                              
      reset = new OutputPort(Cpu.Pin.GPIO_Pin13, true); // Port 6 Pin 6
      loader = new OutputPort((Cpu.Pin)21, true);       // Port 6 Pin 7

      // UART3 on socket 6
      serial = new SerialPort("COM3", 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
      serial.Open();

      Debug.Print("Press button to start flash");
      Thread.Sleep(-1);
    }

    static void button_OnInterrupt(uint data1, uint data2, DateTime time)
    {
      if (doneFlash)
      {
        doneFlash = false;

        flashThread = new Thread(FlashThreadStart);

        flashThread.Start();
      }
    }

    static void FlashThreadStart()
    {
      //display_T35.SimpleGraphics.Clear();

      Debug.Print("Beginning chip reflash...");

      //display_T35.SimpleGraphics.DisplayText("Beginning chip reflash...", myFont, GT.Color.Red, 0, 10); 

      loader.Write(false);

      Thread.Sleep(100);

      reset.Write(false);

      Thread.Sleep(100);

      reset.Write(true);

      Thread.Sleep(400);

      NXPFlashLoader.CommunicationInterface com = new SerialPortToNXP(serial);

      NXP.Programmer p = new NXP.Programmer(com, NXP.Chips.LPC1111, NXP.Frequency.MHz_168);

      uint Xpos = 150;

      Debug.Print("Connecting...");
      //display_T35.SimpleGraphics.DisplayText("Connecting...", myFont, GT.Color.Red, 0, 35); 
      p.Connect();
      Debug.Print("Connected!");
      //display_T35.SimpleGraphics.DisplayText("Connected!", myFont, GT.Color.Red, Xpos, 35); 

      Debug.Print("Erasing...");
      //display_T35.SimpleGraphics.DisplayText("Erasing...", myFont, GT.Color.Red, 0, 55); 
      p.EraseAll();
      Debug.Print("Erased!");
      //display_T35.SimpleGraphics.DisplayText("Erased!", myFont, GT.Color.Red, Xpos, 55); 

      Debug.Print("Converting File...");
      //display_T35.SimpleGraphics.DisplayText("Converting File...", myFont, GT.Color.Red, 0, 75); 
      p.SelectDownloadBuffer(binFile, 0, binFile.Length);
      Debug.Print("Converted!");
      //display_T35.SimpleGraphics.DisplayText("Converted!", myFont, GT.Color.Red, Xpos, 75); 

      Debug.Print("Downloading...");
      //display_T35.SimpleGraphics.DisplayText("Downloading...", myFont, GT.Color.Red, 0, 95); 
      p.Download();
      Debug.Print("Downloaded!");
      //display_T35.SimpleGraphics.DisplayText("Downloaded!", myFont, GT.Color.Red, Xpos, 95);

      Debug.Print("Verifying...");
      //display_T35.SimpleGraphics.DisplayText("Verifying...", myFont, GT.Color.Red, 0, 115); 
      p.Verify();
      Debug.Print("Verified!");
      //display_T35.SimpleGraphics.DisplayText("Verified!", myFont, GT.Color.Red, Xpos, 115); 

      Debug.Print("Board Flash Complete!");
      //display_T35.SimpleGraphics.DisplayText("Touch the screen to flash another board!", myFont, GT.Color.Red, 0, 145);

      doneFlash = true;
    }
  }
}


