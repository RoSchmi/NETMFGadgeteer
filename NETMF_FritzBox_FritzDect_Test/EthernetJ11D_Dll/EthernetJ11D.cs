using System;
using Microsoft.SPOT;
using GHI.Networking;
using System.Threading;
using GHI.Processor;
//using GTI = Gadgeteer.SocketInterfaces;
//using GTM = Gadgeteer.Modules;

namespace RoSchmi.EthernetJ11D
{
    
	/// <summary>An EthernetJ11D module for Microsoft .NET Gadgeteer</summary>
	public class EthernetJ11D //: GTM.Module.NetworkModule
    {
		private EthernetBuiltIn networkInterface;

		/// <summary>The underlying network interface.</summary>
		public EthernetBuiltIn NetworkInterface {
			get {
				return this.networkInterface;
			}
		}
        /*
		/// <summary>Whether or not the cable is inserted into the module. Make sure to also check the NetworkUp property to verify network state.</summary>
		public override bool IsNetworkConnected {
			get {
				return this.networkInterface.CableConnected;
			}
		}
        */
		/// <summary>Constructs a new instance.</summary>
		/// <param name="socketNumber">The socket that this module is plugged in to.</param>
		public EthernetJ11D(GHI.Processor.DeviceType deviceType, int socketNumber) 
        {
            switch (deviceType)
            {
                case GHI.Processor.DeviceType.EMX:
                    {

                    }
                    break;
                case GHI.Processor.DeviceType.G120E:
                    {

                    }
                    break;
                default:
                    {
                        throw new NotSupportedException("Mainboard not supported!");
                    }
            }
            
            /*
			Socket socket = Socket.GetSocket(socketNumber, true, this, null);

			socket.EnsureTypeIsSupported('E', this);

			socket.ReservePin(Socket.Pin.Four, this);
			socket.ReservePin(Socket.Pin.Five, this);
			socket.ReservePin(Socket.Pin.Six, this);
			socket.ReservePin(Socket.Pin.Seven, this);
			socket.ReservePin(Socket.Pin.Eight, this);
			socket.ReservePin(Socket.Pin.Nine, this);
            */
			this.networkInterface = new EthernetBuiltIn();
            /*
			this.NetworkSettings = this.networkInterface.NetworkInterface;
            */
		}

		/// <summary>Opens the underlying network interface and assigns the NETMF networking stack.</summary>
		public void UseThisNetworkInterface() {
			if (this.networkInterface.Opened)
				return;

			this.networkInterface.Open();
		}
	}
}

