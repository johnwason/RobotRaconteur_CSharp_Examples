using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using experimental.create;
using RobotRaconteur;

namespace iRobotCreateService
{

    //This program provides a simple Robot Raconteur server for controlling the iRobot Create.  It uses
    //the Create_interface.robdef service definition.

    class Program
    {
        public static Create create;
        static void Main(string[] args)
        {
            RobotRaconteurNativeLoader.Load();

            //Read the serial port name from program arguments
            string port = args[0];

            //Initialize the create robot object
            create = new Create();
            create.Start(port);

            LocalTransport t1 = new LocalTransport();
            t1.StartServerAsNodeName("experimental.create.Create");
            RobotRaconteurNode.s.RegisterTransport(t1);

            //Initialize the TCP transport and start listening for connections on port 2354
            TcpTransport t2 = new TcpTransport();
            t2.StartServer(2354);

            //Attempt to load TLS certificate
            try
            {
                t2.LoadTlsNodeCertificate();
            }
            catch
            {
                Console.WriteLine("warning: could not load TLS certificate");
            }

            //Enable auto-discovery announcements
            t2.EnableNodeAnnounce();

            //Register the TCP transport
            RobotRaconteurNode.s.RegisterTransport(t2);

            //Register the Create_interface type so that the node can understand the service definition
            RobotRaconteurNode.s.RegisterServiceType(new experimental__createFactory());

            //Register the create object as a service so that it can be connected to
            RobotRaconteurNode.s.RegisterService("Create", "experimental.create", create);

            //Stay open until shut down
            Console.WriteLine("Create server started. Connect with URL rr+tcp://localhost:2354?service=Create Press enter to exit");
            Console.ReadLine();

            //Shutdown
            create.Shutdown();

            //Shutdown the node.  This must be called or the program won't exit.
            RobotRaconteurNode.s.Shutdown();
        }
    }


    //The implementation of the create object.  It implementes Create_interface.Create.  
    //This allows the object to be exposed using the Create_interface
    //service definition.

    public class Create : experimental.create.Create
    {
        SerialPort port;

        object port_lock = new object();
        object recv_port_lock = new object();

        //Initialize the serial port and set the data received callback
        public void Start(string portname)
        {
            lock (port_lock)
            {
                port = new SerialPort(portname, 57600, Parity.None, 8, StopBits.One);
                port.Open();

                port.DataReceived += SerialDataReceived;


                byte[] command = new byte[] { 128, 132 };

                port.Write(command, 0, command.Length);

                try
                {
                    System.Threading.Thread.Sleep(500);
                }
                catch { }

            }

        }

        //Serial event callback
        private void SerialDataReceived(Object sender, SerialDataReceivedEventArgs args)
        {
            if (args.EventType == SerialData.Chars)
            {
                ReceiveSensorPackets();

            }

        }


        public bool lastbump = false;
        bool lastplay=false;

        //Process the data coming from the robot.  This function is largely handling the robot rather than
        //RobotRaconteur
        public void ReceiveSensorPackets()
        {
            try
            {
                lock (recv_port_lock)
                {
                    while (port.BytesToRead > 0)
                    {


                        byte seed = (byte)port.ReadByte();
                       
                        if (seed != 19)
                        {
                            return;
                        }


                        byte nbytes = (byte)port.ReadByte();

                        if (nbytes == 0) return;


                        byte[] packets = new byte[nbytes + 1];

                        int bytesread = 0;
                        while (bytesread < packets.Length)
                        {
                            bytesread += port.Read(packets, bytesread, packets.Length - bytesread);
                        }

                        SendSensorPacket(seed, packets);

                        int readpos = 0;

                        while (readpos < nbytes)
                        {

                            byte id = packets[readpos++];
                            
                            switch (id)
                            {
                                case 7:
                                    {
                                        byte flags = (byte)packets[readpos++];
                                        if (((flags & 0x1) != 0) || ((flags & 0x2) != 0))
                                        {
                                            if (lastbump == false)
                                            {
                                                fire_Bump();
                                            }
                                            lastbump = true;
                                        }
                                        else
                                        {
                                            lastbump = false;
                                        }
                                        m_Bumpers = flags;

                                    }

                                    break;
                                case 19:
                                    {
                                        byte high = (byte)packets[readpos++];
                                        byte low = (byte)packets[readpos++];

                                        byte[] bits = new byte[] { low, high };
                                        m_DistanceTraveled += BitConverter.ToInt16(bits, 0);

                                    }


                                    break;
                                case 20:
                                    {
                                        byte high = (byte)packets[readpos++];
                                        byte low = (byte)packets[readpos++];

                                        byte[] bits = new byte[] { low, high };
                                        m_AngleTraveled += BitConverter.ToInt16(bits, 0);

                                    }
                                    break;
                                case 18:
                                    {
                                        byte buttons=(byte)packets[readpos++];
                                        byte bplay=(byte)(buttons & ((byte)0x1));
                                        if (bplay==1)
                                        {
                                            if (!lastplay)
                                            {
                                                play();
                                            }
                                            lastplay=true;
                                        }
                                        else
                                        {
                                            lastplay=false;
                                        }
                                    }
                                    break;
                                default:

                                    readpos++;
                                    break;
                            }
                        }


                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }


        }

        //Shutdown the robot
        public void Shutdown()
        {
            lock (port_lock)
            {
                if (streaming)
                {
                    StopStreaming();
                    System.Threading.Thread.Sleep(500);

                }


                byte[] command = new byte[] { 128 };
                port.Write(command, 0, command.Length);

                port.DiscardInBuffer();
                port.DiscardOutBuffer();



                port.Close();
            }
        }

        //Drive the robot with given velocity and radius
        public void Drive(short velocity, short radius)
        {
            lock (port_lock)
            {
                byte[] vel = BitConverter.GetBytes(velocity);
                byte[] rad = BitConverter.GetBytes(radius);


                byte[] command = { 137, vel[1], vel[0], rad[1], rad[0] };

                port.Write(command, 0, command.Length);
            }
        }

        //Event that implements the "Bump" event from the Create interface
        public event Action Bump;

        //Fire the bump event notifying all clients
        private void fire_Bump()
        {
            if (Bump != null)
                Bump();
        }

        private bool streaming = false;

        //Stop streaming data from the robot
        public void StopStreaming()
        {
            lock (port_lock)
            {
                byte[] command = new byte[] { 150, 0 };
                port.Write(command, 0, command.Length);
                streaming = false;
            }
        }


        uint current_client=0;

        //Start streaming data from the robot
        public void StartStreaming()
        {
            lock (port_lock)
            {

                byte[] command = new byte[] { 148, 4, 7, 19, 20, 18 };
                port.Write(command, 0, command.Length);
                streaming = true;
                //Retrieve the endpoint of the current client.  This uniquely identifies
                //the current client
                current_client=ServerEndpoint.CurrentEndpoint;
            }
        }

        private int m_AngleTraveled = 0;
        private int m_DistanceTraveled = 0;

        //Property for AngleTraveled
        public int AngleTraveled
        {
            get
            {
                if (!streaming) throw new InvalidOperationException("Not receiving data");
                return m_AngleTraveled;
            }
            set { throw new InvalidOperationException("Read only property"); }
        }

        //Property for DistanceTraveled
        public int DistanceTraveled
        {
            get
            {
                if (!streaming) throw new InvalidOperationException("Not receiving data");
                return m_DistanceTraveled;
            }
            set { throw new InvalidOperationException("Read only property"); }
        }


        //Field and property for the wire.  This should be nearly identical for any use of wires with the difference
        //being the name of the wire and the type of packet.
        private RobotRaconteur.Wire<SensorPacket> _packets;
        public RobotRaconteur.Wire<SensorPacket> packets
        {
            get
            {
                return _packets;
            }
            set
            {
                if (_packets != null) throw new InvalidOperationException("Pipe has already been set");
                _packets = value;
                //Set the connect callback when clients connect to the wire
                _packets.WireConnectCallback = WireConnectCallbackFunction;
            }

        }

        Dictionary<uint, Wire<SensorPacket>.WireConnection> wireconnections = new Dictionary<uint, Wire<SensorPacket>.WireConnection>();

        //When a wire connects connects, add it to the dictionary indexed by endpoint
        void WireConnectCallbackFunction(Wire<SensorPacket> w, Wire<SensorPacket>.WireConnection wire)
        {


            lock (wireconnections)
            {

                wireconnections.Add(wire.Endpoint, wire);
            }
            wire.WireCloseCallback = WireClosedCallbackFunction;

        }


        //Callback when a wire closes
        void WireClosedCallbackFunction(Wire<SensorPacket>.WireConnection wire)
        {
            lock (wireconnections)
            {
                wireconnections.Remove(wire.Endpoint);
            }
        }

        //Cycle through all the wire connections and send the SensorPacket.  If there is an error, close the wire connection.
        void SendSensorPacket(byte id, byte[] data)
        {


            SensorPacket p = new SensorPacket();
            p.ID = id;
            p.Data = data;

            lock (wireconnections)
            {
                uint[] ep = wireconnections.Keys.ToArray();

                foreach (uint e in ep)
                {
                    
                        Wire<SensorPacket>.WireConnection wend = wireconnections[e];

                        try
                        {
                            wend.OutValue = p;



                        }
                        catch
                        {
                            try
                            {
                                wend.Close();
                            }
                            catch { }

                            try
                            {
                                wireconnections.Remove(e);
                            }
                            catch { }
                        }                   

                }



            }


        }



        byte m_Bumpers;
        //Property for the bumpers
        public byte Bumpers
        {
            get
            {
                return m_Bumpers;
            }
            set
            {
                throw new InvalidOperationException("Read only property");
            }
        }

        
        Callback<Func<int, int, byte[]>> _play_callback;
        
        //Property to store the callback server
        public Callback<Func<int, int, byte[]>> play_callback {
            get
            {
                return _play_callback;
            }

            set
            {
                _play_callback=value;
            }
        }


        //Function to call the "play" callback.
        private void play()
        {
            //If we don't have a current client, return
            if (current_client == 0) return;

            //Retrieve and execute the function indexed by the current_client endpoint
            //that uniquely identifies the client
            //GetClientFunction returns a delegate that calls the client
            byte[] notes = play_callback.GetClientFunction(current_client)(m_DistanceTraveled, m_AngleTraveled);

            byte[] command = new byte[notes.Length + 5];
            command[0] = 140;
            command[1] = 0;
            command[2] = (byte)(notes.Length / 2);
            Array.Copy(notes, 0, command, 3, notes.Length);
            command[3 + notes.Length] = 141;
            command[4 + notes.Length] = 0;

            lock (port_lock)
            {
                port.Write(command,0,command.Length);
            }


        }


    }
}

