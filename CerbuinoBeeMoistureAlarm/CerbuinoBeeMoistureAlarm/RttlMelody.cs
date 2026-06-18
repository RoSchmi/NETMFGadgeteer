using System;
using Microsoft.SPOT;
using Gadgeteer.Modules.GHIElectronics;

namespace CerbuinoBeeMoistureAlarm
//namespace   Gadgeteer.Modules.GHIElectronics
{
    class RttlMelody
    {
private readonly float[] Octave1 =
         {
             0.0f,      // p
             32.703f,   // c
             34.648f,   // c#
             36.708f,   // d
             38.891f,   // d#
             41.203f,   // e
             43.654f,   // f
             46.249f,   // f#
             48.999f,   // g
             51.913f,   // g#
             55.000f,   // a
             58.270f,   // a#
             61.735f    // b
         };

         public string Name { get; set; }
         public int Duration { get; set; }
         public int Octave { get; set; }
         public int Beat { get; set; }
         public int Tempo { get; set; }

         private string[] Song;

         public RttlMelody(string rttl)
         {
             // split on ':'
             var parts = rttl.Split(':');
             Name = parts[0];

             // split on ','
             var header = parts[1].Split(',');

             // calculate defaults
             Duration = int.Parse(header[0].Substring(2));
             Octave = int.Parse(header[1].Substring(2));
             Beat = int.Parse(header[2].Substring(2));

             Tempo = ((1000 * 60) / Beat) * 4;  // not sure about this

             // split on ','
             Song = parts[2].Split(',');
         }

         private Tunes.Melody melody;

         public Tunes.Melody ToMelody()
         {
             melody = new Tunes.Melody();

             foreach (var n in Song)
             {
                 AddNote( n.ToLower().Trim() );
             }

             return melody;
         }

         private const string digits = "0123456789";

         private char Next(string token, int index)
         {
             if (index < token.Length)
                 return token[index];
             return '?';
         }

         private void AddNote(string token)
         {
             // set up defaults
             int note = 0;
             int length = Duration;
             int octave = Octave;
             char c = Next( token, 0 );
             int p = 1;

             // check for leading duration
             int i = digits.IndexOf(c);
             if (i >= 1)
             {
                 length = i;

                 c = Next(token, p++);
                 i = digits.IndexOf(c);
                 if (i >= 0)
                 {
                     length = length * 10 + i;
                     c = Next(token, p++);
                 }
             }

             // check the note
             i = "pc.d.ef.g.a.b".IndexOf(c);
             if (i >= 0)
             {
                 note = i;
                 c = Next(token, p++);

                 if (c == '#')
                 {
                     ++note;
                     c = Next(token, p++);
                 }

                 if (c == '.')
                 {
                     length += length/2;
                     c = Next(token, p); //++);
                 }

                 // check for octave specifier
                 i = digits.IndexOf(c);
                 if (i >= 1)  // && < 6??
                 {
                     octave = i;
                 }
             }

             AddNote( note, length, octave );
         }


         private void AddNote(int note, int length, int octave)
         {
             // convert note into frequency
             double f = Octave1[note] * (1 << (octave-1));

             // calculate duration in ms
             int ms = Tempo / length;

             // add to the melody
             //Console.WriteLine("{0} {1} {2} {3} {4}", note, length, octave, f, ms );
             melody.Add(new Tunes.Tone(f), ms);
         }

    }
}


/*
using System;
using Microsoft.SPOT;
using Gadgeteer.Modules.GHIElectronics;



//namespace Tunes_Test_2013_R3
 namespace   Gadgeteer.Modules.GHIElectronics

{
    public class RttlMelody
    {
         private readonly float[] Octave1 =
         {
             0.0f,      // p
             32.703f,   // c
             34.648f,   // c#
             36.708f,   // d
             38.891f,   // d#
             41.203f,   // e
             43.654f,   // f
             46.249f,   // f#
             48.999f,   // g
             51.913f,   // g#
             55.000f,   // a
             58.270f,   // a#
             61.735f    // b
         };

         public string Name { get; set; }
         public int Duration { get; set; }
         public int Octave { get; set; }
         public int Beat { get; set; }
         public int Tempo { get; set; }

         private string[] Song;

         public RttlMelody(string rttl)
         {
             // split on ':'
             var parts = rttl.Split(':');
             Name = parts[0];

             // split on ','
             var header = parts[1].Split(',');

             // calculate defaults
             Duration = int.Parse(header[0].Substring(2));
             Octave = int.Parse(header[1].Substring(2));
             Beat = int.Parse(header[2].Substring(2));

             Tempo = ((1000 * 60) / Beat) * 4;  // not sure about this

             // split on ','
             Song = parts[2].Split(',');
         }

         private Tunes.Melody melody;

         public Tunes.Melody ToMelody()
         {
             melody = new Tunes.Melody();

             foreach (var n in Song)
             {
                 AddNote( n.ToLower().Trim() );
             }

             return melody;
         }

         private const string digits = "0123456789";

         private char Next(string token, int index)
         {
             if (index < token.Length)
                 return token[index];
             return '?';
         }

         private void AddNote(string token)
         {
             // set up defaults
             int note = 0;
             int length = Duration;
             int octave = Octave;
             char c = Next( token, 0 );
             int p = 1;

             // check for leading duration
             int i = digits.IndexOf(c);
             if (i >= 1)
             {
                 length = i;

                 c = Next(token, p++);
                 i = digits.IndexOf(c);
                 if (i >= 0)
                 {
                     length = length * 10 + i;
                     c = Next(token, p++);
                 }
             }

             // check the note
             i = "pc.d.ef.g.a.b".IndexOf(c);
             if (i >= 0)
             {
                 note = i;
                 c = Next(token, p++);

                 if (c == '#')
                 {
                     ++note;
                     c = Next(token, p++);
                 }

                 if (c == '.')
                 {
                     length += length/2;
                     c = Next(token, p); //++);
                 }

                 // check for octave specifier
                 i = digits.IndexOf(c);
                 if (i >= 1)  // && < 6??
                 {
                     octave = i;
                 }
             }

             AddNote( note, length, octave );
         }


         private void AddNote(int note, int length, int octave)
         {
             // convert note into frequency
             double f = Octave1[note] * (1 << (octave-1));

             // calculate duration in ms
             int ms = Tempo / length;

             // add to the melody
             //Console.WriteLine("{0} {1} {2} {3} {4}", note, length, octave, f, ms );
             melody.Add(new Tunes.Tone(f), ms);
         }

     }

}
 */

