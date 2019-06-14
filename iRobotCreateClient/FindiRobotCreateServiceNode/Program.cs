using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.create2;
using System.Threading;

namespace FindiRobotCreateServiceNode
{
    //This program uses the FindServiceByType function to find the iRobot Create service
    //using autodiscovery
    class Program
    {
        static void Main(string[] args)
        {

            //Use ClientNodeSetup to configure the node
            using (new ClientNodeSetup())
            {                
                //Wait 5 seconds to receive the discovery packets
                Thread.Sleep(5000);

                //Search for the "Create_interface.Create" object type on "tcp" transports
                ServiceInfo2[] res = RobotRaconteurNode.s.FindServiceByType("experimental.create2.Create", new string[] { "rr+local", "rr+tcp" });
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
                    Create c = (Create)RobotRaconteurNode.s.ConnectService(res[0].ConnectionURL, null, null, null, "experimental.create2.Create");

                    //Drive a bit
                    c.Drive(200, 5000);
                    Thread.Sleep(1000);
                    c.Drive(0, 0);
                }
            }                      
            
        }
    }
}
