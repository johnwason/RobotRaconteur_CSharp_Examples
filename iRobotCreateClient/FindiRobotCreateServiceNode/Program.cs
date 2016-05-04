using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.create;
using System.Threading;

namespace FindiRobotCreateServiceNode
{
    //This program uses the FindServiceByType function to find the iRobot Create service
    //using autodiscovery
    class Program
    {
        static void Main(string[] args)
        {
            //Load the native part of the software
            RobotRaconteurNativeLoader.Load();

            //Create and register a TcpTransport
            TcpTransport t = new TcpTransport();
            RobotRaconteurNode.s.RegisterTransport(t);

            //Enable the TcpChannel to listen for other nodes.  The IPNodeDiscoveryFlags will
            //normally be the same as here
            t.EnableNodeDiscoveryListening();

            //Register the Create_interface service type
            RobotRaconteurNode.s.RegisterServiceType(new experimental__createFactory());

            //Wait 10 seconds to receive the beacon packets for autodiscovery which are
            //sent every 5 seconds.
            Thread.Sleep(10000);

            //Search for the "Create_interface.Create" object type on "tcp" transports
            ServiceInfo2[] res = RobotRaconteurNode.s.FindServiceByType("experimental.create.Create", new string[] { "local","tcp" });
            foreach (ServiceInfo2 r in res)
            {
                Console.WriteLine(r.NodeName + " " + r.NodeID.ToString() + " " + r.Name + " " + r.ConnectionURL[0]);
            }


            if (res.Length == 0)
            {
                Console.WriteLine("Create not found.");
            }
            else
            {
                //Connect to the found service
                Create c = (Create)RobotRaconteurNode.s.ConnectService(res[0].ConnectionURL, null, null, null, "experimental.create.Create");

                //Drive a bit
                c.Drive(200, 5000);
                Thread.Sleep(1000);
                c.Drive(0, 0);
            }
                       
            //Shutdown Robot Raconteur
            RobotRaconteurNode.s.Shutdown();
        }
    }
}
