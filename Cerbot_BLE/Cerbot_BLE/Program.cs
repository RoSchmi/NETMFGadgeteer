// NETMF 4.3 GHI SDK 2016 R1
// Demonstrates the basics of programming FEZ Cerbot using Justin's Bluettooth SMART Module
// This programm is an an adaption of Peters Cerbot App
// https://www.ghielectronics.com/community/codeshare/entry/1018
//
// Last modified 2026
// A Net.Maui App e.g. MauiBluetoothCerbotContoller
// https://github.com/RoSchmi/MauiBluetoothCerbotController
// can connect the the FEZ-Cerbot, when Justins Bluetooth SMART Module is connected to socket 5
// The combination of the App and MauiBluetoothCerbotContoller on a Windows PC lets you control the motors
// And enables you playing some melodies


using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;
using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.IngenuityMicro;

namespace Cerbot_BLE
{
    public partial class Program
    {
        const string ProgramProperties =
            "\nProgram: Cerbot_BLE \nVersion: 1.1.0 \nDate: 27.06.26" +
            "\nVisual Studio 2013, Platform: Gadgeteer, GHI NETMF 4.3 2016 R1 (4.3.8.1)" +
            "\nMainboard Cerberus on FEZ-Cerbot";

        bool DebugLed = false;
        GTM.IngenuityMicro.BluetoothSmart bleSerial;
        
        static GHIElectronics.Gadgeteer.FEZCerbot myFEZCerbot;

        #region Notes

        const double C3 = 130.8128f;
        const double C3S = 138.5913f;
        const double D3 = 146.8324f;
        const double D3S = 155.5635f;
        const double E3 = 164.8138f;
        const double F3 = 174.6141f;
        const double F3S = 184.9972f;
        const double G3 = 195.9977f;
        const double G3S = 207.6523f;
        const double A3 = 220.0000f;
        const double A3S = 233.0819f;
        const double B3 = 246.9417f;
        const double C4 = 261.6256f;
        const double C4S = 277.1826f;
        const double D4 = 293.6648f;
        const double D4S = 311.127f;
        const double E4 = 329.6276f;
        const double F4 = 349.2282f;
        const double F4S = 369.9944f;
        const double G4 = 391.9954f;
        const double G4S = 415.3047f;
        const double A4 = 440.0000f;
        const double A4S = 466.1638f;
        const double B4 = 493.8833f;
        const double C5 = 523.2511f;
        const double C5S = 554.3653f;
        const double D5 = 587.3295f;
        const double D5S = 622.254f;
        const double E5 = 659.2551f;
        const double F5 = 698.4565f;
        const double F5S = 739.9888f;
        const double G5 = 783.9909f;
        const double G5S = 830.6094f;
        const double A5 = 880.0000f;
        const double A5S = 932.3275f;
        const double B5 = 987.7666f;
        const double C6 = 1046.502;
        const double C6S = 1108.731f;
        const double D6 = 1174.659f;
        const double D6S = 1244.508f;
        const double E6 = 1318.51f;
        const double F6 = 1396.913f;
        const double F6S = 1479.978f;
        const double G6 = 1567.982f;
        const double G6S = 1661.219f;
        const double A6 = 1760.0000f;
        const double A6S = 1864.655f;
        const double B6 = 1975.533f;
        const double C7 = 2093.005f;
        const double RR = 0.0f;

        const int WHOLE_DURATION = 1000;
        const int SIXTEENTH = WHOLE_DURATION / 16;
        const int EIGHTH = WHOLE_DURATION / 8;
        const int EIGHTHDOT = WHOLE_DURATION / 6;
        const int QUARTER = WHOLE_DURATION / 4;
        const int QUARTERDOT = WHOLE_DURATION / 3;
        const int HALF = WHOLE_DURATION / 2;
        const int WHOLE = WHOLE_DURATION;

        double[] note = { E4, E4, F4, G4, G4, F4, E4,
                          D4, C4, C4, D4, E4, E4, D4,
                          D4, E4, E4, F4, G4, G4, F4,
                          E4, D4, C4, C4, D4, E4, D4,
                          C4, C4};

        uint[] duration = { QUARTER, QUARTER, QUARTER, QUARTER, QUARTER, QUARTER,    QUARTER,
                                  QUARTER, QUARTER, QUARTER, QUARTER, QUARTER, QUARTERDOT, EIGHTH,
                                  HALF,    QUARTER, QUARTER, QUARTER, QUARTER, QUARTER,    QUARTER,
                                  QUARTER, QUARTER, QUARTER, QUARTER, QUARTER, QUARTER,    QUARTERDOT,
                                  EIGHTH,  WHOLE};

        double[] tor_note = { C4, D4, C4, A3, A3,
                              A3, G3, A3, A3S, A3, RR,
                              A3S, G3, C4, A3, RR,
                              F3, D3, G3, C3, RR,
                              G3, D4, C4, A3S,
                              A3, G3, A3, A3S, A3, RR,
                              E3, A3, A3, G3S, B3, 
                              E4,
                              RR, D4, C4S, D4, G3, A3, A3S,
                              RR, A3, F3, D4, C4,
                              RR, F3, C3, A3S, A3, G3,
                              F3, RR, RR
                            };

        uint[] tor_duration = { QUARTER, EIGHTHDOT, SIXTEENTH, QUARTER, QUARTER,
                                EIGHTHDOT, SIXTEENTH, EIGHTHDOT, SIXTEENTH, QUARTER, QUARTER,
                                QUARTER, EIGHTHDOT, SIXTEENTH, QUARTER, QUARTER,
                                QUARTER, EIGHTHDOT, SIXTEENTH, QUARTER, QUARTER,
                                HALF + EIGHTH, EIGHTH, EIGHTH, EIGHTH,
                                EIGHTH, EIGHTH, EIGHTH, EIGHTH, QUARTER, QUARTER,
                                QUARTER, QUARTER, QUARTER, EIGHTH, EIGHTH,
                                WHOLE,
                                EIGHTH, EIGHTH, EIGHTH, EIGHTH, EIGHTH, EIGHTH, QUARTER,
                                EIGHTH, EIGHTH, EIGHTH, EIGHTH, HALF,
                                EIGHTH, EIGHTH, EIGHTH, EIGHTH, QUARTER, QUARTER,
                                QUARTER, QUARTER, HALF
                              };

        double[] pic_note = { A4, RR, A4, B4, C5, A4,
                              E4, RR, E4, F4, E4,
                              D4, RR, D4, E4, F4, D4, B3,
                              F4, RR, F4, G4, F4, F4,
                              A4, RR, A4, B4, C5, A4,
                              E4, RR, E4, F4, E4,
                              D4, RR, D4, E4, F4, D4, B3,
                              F4
                            };

        uint[] pic_duration = { QUARTER, EIGHTH, EIGHTH, EIGHTH, EIGHTH, QUARTER,
                                QUARTER, EIGHTH, EIGHTH, QUARTER, QUARTER,
                                QUARTER, EIGHTH, EIGHTH, EIGHTH, EIGHTH, EIGHTH, EIGHTH,
                                QUARTER, EIGHTH, EIGHTH, EIGHTH, EIGHTH, QUARTER,
                                QUARTER, EIGHTH, EIGHTH, EIGHTH, EIGHTH, QUARTER,
                                QUARTER, EIGHTH, EIGHTH, QUARTER, QUARTER,
                                QUARTER, EIGHTH, EIGHTH, EIGHTH, EIGHTH, EIGHTH, EIGHTH,
                                HALF
                              };

        #endregion

        #region ProgramStarted
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            Thread.Sleep(1000);
            Debug.Print(ProgramProperties);

            myFEZCerbot = Program.Mainboard;

            bleSerial = new GTM.IngenuityMicro.BluetoothSmart(5);
            bleSerial.DataReceived += bleSerial_DataReceived;

            GT.Timer blinky = new GT.Timer(1000); // every second (1000ms)
            blinky.Tick += blinky_Tick;
            blinky.Start();

            #region Use OnBoard LEDs

            // using directly Program.Mainboard
            myFEZCerbot.TurnOnLed(1);
            Thread.Sleep(200);
            myFEZCerbot.TurnOffLed(1);
            Thread.Sleep(200);
            myFEZCerbot.TurnOnLed(1);
            Thread.Sleep(200);
            myFEZCerbot.TurnOffLed(1);
            Thread.Sleep(200);
            myFEZCerbot.TurnOnLed(1);
            Thread.Sleep(200);
            myFEZCerbot.TurnOffLed(1);
            Thread.Sleep(200);
            #endregion

            // Play a startup tune
            new Thread(Tunes).Start();
            

            // See if we can start a sweep of the front LEDs
            new Thread(Sweep).Start();

            Debug.Print("Program Started");
        }
        #endregion

        #region Looney Tunes

        void Tunes()
        {
            // Play simple tune
            SimpleTune();
            //SimpleSirene();
            //SimpleToreador();
            //SimplePrayerInC();
        }

        private void SimpleTune()
        {
            
                myFEZCerbot.StartBuzzer(F5, 250);
                myFEZCerbot.StopBuzzer();
           
        }

        private void SimpleSirene()
        {
            // Sweep frequency from 635 to 912 in ? steps
            // duty cycle (volume, to simulate doppler effect)
            // 912 = .4 duty cycle 
            // 635 = .5 duty cycle = max volume
            double lo = 635.00;
            double hi = 912.00;
            double play = lo;

            for (int j = 0; j < 3; j++)
            {
                while (play <= hi)
                {
                    myFEZCerbot.StartBuzzer(play);
                    Thread.Sleep(20);
                    myFEZCerbot.StopBuzzer();
                    play += 1;
                }
            }

        }

        private void SimpleToreador()
        {

            for (int i = 0; i < tor_note.Length; i++)
            {
                if (tor_note[i] == RR)
                {
                    myFEZCerbot.StopBuzzer();
                    Thread.Sleep((int)tor_duration[i] * 2);
                }
                else
                {
                    myFEZCerbot.StartBuzzer(tor_note[i]);
                    Thread.Sleep((int)tor_duration[i] * 2);
                    myFEZCerbot.StopBuzzer();
                }
            }
        }

        private void SimplePrayerInC()
        {

            for (int i = 0; i < pic_note.Length; i++)
            {
                if (pic_note[i] == RR)
                {
                    myFEZCerbot.StopBuzzer();
                    Thread.Sleep((int)(pic_duration[i] * 1.8));
                }
                else
                {
                    myFEZCerbot.StartBuzzer(pic_note[i]);
                    Thread.Sleep((int)(pic_duration[i] * 1.8));
                    myFEZCerbot.StopBuzzer();
                }
            }
        }
        #endregion


        #region bleSerial_DataReceived
        void bleSerial_DataReceived(string val)
        {
            string[] cmds = val.Split(':');

            Debug.Print("Commands coming in: " + cmds.Length);
            for (int i = 0; i < cmds.Length; i++)
            {
                Debug.Print(cmds[i]);
            }

            switch (cmds[0].ToUpper())
            {
                case "S": // Stop
                    myFEZCerbot.SetMotorSpeed(0, 0);
                    break;
                case "F": // Force 100 = max forward and -100 = max backward
                    try
                    {
                        int left = Convert.ToInt16(cmds[1]);
                        if (left > 100) left = 100;
                        if (left < -100) left = -100;

                        int right = Convert.ToInt16(cmds[2]);
                        if (right > 100) right = 100;
                        if (right < -100) right = -100;

                        myFEZCerbot.SetMotorSpeed(left, right);
                    }
                    catch (Exception ex)
                    {
                        // Beep three times
                        CerbotError();
                        // And show in output window
                        Debug.Print(ex.Message);

                    }
                    break;
                case "T":
                    // Get the tune number
                    int tune = Convert.ToInt16(cmds[1]);
                    // Get the loop count
                    int times = Convert.ToInt16(cmds[2]);
                    // Play it
                    for (int i = 0; i < times; i++)
                    {
                        switch (tune)
                        {
                            case 1:
                                SimpleTune();  
                                break;
                            case 2:
                                SimpleToreador();
                                break;
                            case 3:
                                SimplePrayerInC();
                                break;
                            case 4:
                                SimpleSirene();
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        #endregion

        private void CerbotError()
        {
            // First set motor to stop
            myFEZCerbot.SetMotorSpeed(0, 0);
            // Play simple error tune
            SimpleTune();

        }

        void blinky_Tick(GT.Timer timer)
        {
            DebugLed = !DebugLed;
            myFEZCerbot.SetDebugLED(DebugLed);
        }

        void Sweep()
        {
            ushort Eyes = 0;
            int direction = 1;
            int ledIndex = 0;
            while (true)
            {
                ledIndex += direction;
                Eyes = (ushort)(0x1 << ledIndex);
                myFEZCerbot.SetLedBitmask(Eyes);
                Thread.Sleep(30);

                if (ledIndex <= 0 || ledIndex >= 15)
                {
                    direction *= -1;
                    // myFEZCerbot.StartBuzzer(1000);
                    Thread.Sleep(20);
                    //myFEZCerbot.StopBuzzer();
                }
            }
        }
    }
}
