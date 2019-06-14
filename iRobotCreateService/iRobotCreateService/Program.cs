using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using experimental.create2;
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

            //Read the serial port name from program arguments
            string port = args[0];

            //Initialize the create robot object
            create = new Create();
            create.Start(port);

            //Use ServerNodeSetup to initialize server node
            using (new ServerNodeSetup("experimental.create2", 2354))
            {
                //Register the create object as a service so that it can be connected to
                RobotRaconteurNode.s.RegisterService("Create", "experimental.create2", create);

                //Stay open until shut down
                Console.WriteLine("Create server started. Connect with URL rr+tcp://localhost:2354?service=Create Press enter to exit");
                Console.ReadLine();

                //Shutdown
                create.Shutdown();
            }
        }
    }

    //The implementation of the create object.  It implementes Create_interface.Create.  
    //This allows the object to be exposed using the Create_interface
    //service definition.

    public class Create : experimental.create2.Create_default_impl
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
        public override void Drive(short velocity, short radius)
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
        public override event Action Bump;

        //Fire the bump event notifying all clients
        private void fire_Bump()
        {
            if (Bump != null)
                Bump();
        }

        private bool streaming = false;

        //Stop streaming data from the robot
        public override void StopStreaming()
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
        public override void StartStreaming()
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
        public override int AngleTraveled
        {
            get
            {
                if (!streaming) throw new InvalidOperationException("Not receiving data");
                return m_AngleTraveled;
            }
            set { throw new InvalidOperationException("Read only property"); }
        }

        //Property for DistanceTraveled
        public override int DistanceTraveled
        {
            get
            {
                if (!streaming) throw new InvalidOperationException("Not receiving data");
                return m_DistanceTraveled;
            }
            set { throw new InvalidOperationException("Read only property"); }
        }
        
       
        void SendSensorPacket(byte id, byte[] data)
        {
            
            SensorPacket p = new SensorPacket();
            p.ID = id;
            p.Data = data;

            if (rrvar_packets!=null)
            {
                rrvar_packets.OutValue = p;
            }
        }



        byte m_Bumpers;
        //Property for the bumpers
        public override byte Bumpers
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

