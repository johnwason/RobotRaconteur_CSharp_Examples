using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.create2;
using System.Threading;

namespace iRobotCreateClient
{

    //This program provides a simple client to the iRobotCreate service
    //that connects, drives a bit, and then disconnects

    class Program
    {
        static void Main(string[] args)
        {
            //Use ClientNodeSetup to configure the node
            using (new ClientNodeSetup())
            {                
                //Connect to the service
                Create c = (Create)RobotRaconteurNode.s.ConnectService("rr+tcp://localhost:2354/?service=Create", null, null, null, "experimental.create2.Create");

                //Start streaming data to this client
                c.StartStreaming();

                //Set an event handler for the "Bump" event
                c.Bump += Bump;

                //Connect the "packets" wire and add a value changed event handler
                Wire<SensorPacket>.WireConnection wire = c.packets.Connect();
                wire.WireValueChanged += wire_changed;

                //Set a function to be used by the callback.  This function will be called
                //when the service calls a callback with the endpoint corresponding to this
                //client
                c.play_callback.Function = play_callback;

                //Drive a bit
                c.Drive(200, 5000);
                Thread.Sleep(1000);
                c.Drive(0, 0);
                Thread.Sleep(20000);

                //Close the wire and stop streaming data
                wire.Close();
                c.StopStreaming();
            }

        }

        //Function to handle the "Bump" event
        static void Bump()
        {
            Console.WriteLine("Bump");

        }

        //Function to handle when the wire value changes
        static void wire_changed( Wire<SensorPacket>.WireConnection wire_connection, SensorPacket value, TimeSpec time)
        {
            SensorPacket value2 = wire_connection.InValue;
            Console.WriteLine(value2.ID);

        }

        //Function that is called by the service as a callback.  This returns
        //a few notes to play.
        static byte[] play_callback(int dist, int angle)
        {
            return new byte[] { 69, 16, 60, 16, 69, 16 };

        }


    }
}
