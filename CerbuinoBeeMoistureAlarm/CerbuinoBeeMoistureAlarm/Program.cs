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
using Gadgeteer.Modules.GHIElectronics;




namespace CerbuinoBeeMoistureAlarm
{
    public partial class Program
    {
        const string ProgramProperties = 
            "\nProgram: CerbiunoBeeMoistureAlarm \nVersion: 1.1.0 \nDate: 18.06.26" +  
            "\nVisual Studio 2013, Platform: Gadgeteer, GHI NETMF 4.3 2016 R1 (4.3.8.1)" + 
            "\nMainboard Cerbuino Bee";
        
        const string BoardName = "Cerbuino Bee";
        GT.Timer timer = new GT.Timer(5000); // every second (1000ms)

        RttlMelody melody;
        Tunes.Tone myTone = new Tunes.Tone(1200);
        Tunes.MusicNote myNote;
        // see -http://www.cellringtones.com/

        public string Barbie = "BarbieGirl:d=4,o=5,b=125:8g#,8e,8g#,8c#6,a,p,8f#,8d#,8f#,8b,g#,8f#,8e,p,8e,8c#,f#,c#,p,8f#,8e,g#,f#";
        public string HauntHouse = "HauntHouse:d=4,o=5,b=108: 2a4, 2e, 2d#, 2b4, 2a4, 2c, 2d, 2a#4, 2e., e, 1f4, 1a4, 1d#, 2e., d, 2c., b4, 1a4, 1p, 2a4, 2e, 2d#, 2b4, 2a4, 2c, 2d, 2a#4, 2e., e, 1f4, 1a4, 1d#, 2e., d, 2c., b4, 1a4";
        public string Christmas = "Christmas:d=4,o=6,b=112:2g5,e.5,8f5,g5,2c,8b5,8c,d,c,b5,a5,2g.5,8b5,8c,d,c,b5,a5,8g5,c,e.5,8g5,8a5,g5,f5,e5,d5,2g.5,2g5,e.5,8f5,g5,2c,8b5,8c,d,c,b5,a5,2g.5,8b5,8c,d,c,b5,a5,8g5,c,e.5,8g5";
        public string Tschaikovs = "Tschaikovs:d=4,o=6,b=200:a_5,8p,8a_5,8a_5,8a_5,c,8p,8c,p,d,8p,8a_5,p,2c,p,a_5,8p,8a_5,8a_5,8a_5,c,8p,8c,p,d,8p,8a_5,p,2c,p,g_5,8a_5,8g_5,8p,8g5,f5,8d_5,8d5,8p,8a_5,g5,8g_5,8g5,8p,8f5,d_5,8d5,8c5,8p,8d_5,d5,8c5,8b5,8p,8f5,d_5,8d5,8c5,8p,8g5,g_5,8g5,8f5,8p,8d_5,a_5,2p,a_5,8p,8a_5,8a_5,8a_5,c,8p,8c,p,d,8p,8a_5,p,2c,p,a_5,8p,8a_5,8a_5,8a_5,c,8p,8c,p,d,8p,8a_5,p,2c";
        public string PhantomOf = "PhantomOf:d=4,o=6,b=100:e5,a5,e5,g5,8f5,2f5,d5,g5,8d5,1e5,e5,a5,e5,g5,8f5,2f5,d5,g5,8d5,1e5,e5,a5,c,e,8d,2d,d,g,8d,1e,e,1a,8g,8f,8e,8d,8c,8b5,8a5,1g_5,f5,f5,8e5,1e5";
        public string Alarm = "Alarm:d=4,o=6,b=100:e5,a5,e5,g5,8f5,2f5,d5,g5,1e5,e5,a5,e5,g5,8f5,2f5,d5,g5,8d5,1e5"; //,e5,a5,c,e,8d,2d,d,g,8d,1e,e,1a,8g,8f,8e,8d,8c,8b5,8a5,1g_5,f5,f5,8e5,1e5";
        public string Init = "Init:d=4,o=6,b=100:e5,a5";

        public bool DoPlay = true;
        


        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            /*******************************************************************************************
            Modules added in the Program.gadgeteer designer view are used by typing 
            their name followed by a period, e.g.  button.  or  camera.
            
            Many modules generate useful events. Type +=<tab><tab> to add a handler to an event, e.g.:
                button.ButtonPressed +=<tab><tab>
            
            If you want to do something periodically, use a GT.Timer and handle its Tick event, e.g.:
                GT.Timer timer = new GT.Timer(1000); // every second (1000ms)
                timer.Tick +=<tab><tab>
                timer.Start();
            *******************************************************************************************/


            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Thread.Sleep(1000);
            Debug.Print(ProgramProperties);

            // melody = new RttlMelody(Barbie);
            // melody = new RttlMelody(Christmas);
            // melody = new RttlMelody(PhantomOf);
            // melody = new RttlMelody(HauntHouse);
            melody = new RttlMelody(Init);
            Tunes.Melody myMelody = melody.ToMelody();



            tunes.Play(myMelody);
            timer.Tick += timer_Tick;
            timer.Start();

            button.ButtonPressed += button_ButtonPressed;

            //myNote = new Tunes.MusicNote(myTone, 2000);
            //tunes.Play(myTone);
            //Thread.Sleep(3000);
            //tunes.Pause();
        }

             void button_ButtonPressed(Button sender, Button.ButtonState state)
        {

            DoPlay = !DoPlay;
        }

        void timer_Tick(GT.Timer timer)
        {
            Debug.Print(moisture.ReadMoisture().ToString());
            //if ((moisture.ReadMoisture() > 10) && DoPlay)
            //if ((moisture.ReadMoisture() > 20) && DoPlay)
                if ((moisture.ReadMoisture() > 100) && DoPlay)
                {
                    if ((moisture.ReadMoisture() > 100) && DoPlay)
                    {
                        melody = new RttlMelody(Init);
                        Tunes.Melody myMelody = melody.ToMelody();
                        tunes.Play(myMelody);
                    }
                }
        }


        }
    }

