using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.create;
using System.Threading;

namespace iRobotCreateClient
{

    //This program provides a simple client to the iRobotCreate service
    //that connects, drives a bit, and then disconnects

    class Program
    {
        static void Main(string[] args)
        {
            //Load the native part of the Robot Raconteur software.  This must be called
            //before any other Robot Raconteur commands
            RobotRaconteurNativeLoader.Load();

            //Create and register a TcpTransport
            TcpTransport t = new TcpTransport();
            RobotRaconteurNode.s.RegisterTransport(t);
            //Register the Create_interface service type
            RobotRaconteurNode.s.RegisterServiceType(new experimental__createFactory());

            //Connect to the service
            Create c = (Create)RobotRaconteurNode.s.ConnectService("tcp://localhost:2354/{0}/Create",null,null,null,"experimental.create.Create");

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
            
            //Shutdown Robot Raconteur.  This MUST be called on exit or the program will crash
            RobotRaconteurNode.s.Shutdown();

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
