using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.createwebcam;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;

namespace SimpleWebcamClient_streaming
{
    //Simple client to read streaming images from the Webcam pipe to show
    //a live view from the cameras

    class Program
    {
        static void Main(string[] args)
        {
            //Load the native part of Robot Raconteur
            RobotRaconteurNativeLoader.Load();

            //Register the service type
            RobotRaconteurNode.s.RegisterServiceType(new experimental__createwebcamFactory());

            //Register the transport
            TcpTransport t = new TcpTransport();
            RobotRaconteurNode.s.RegisterTransport(t);

            //Connect to the service
            WebcamHost c_host = (WebcamHost)RobotRaconteurNode.s.ConnectService("rr+tcp://localhost:2355?service=Webcam", objecttype: "experimental.createwebcam.WebcamHost");

            //Get the Webcam object from the "Webcams" objref
            Webcam c = c_host.get_Webcams(0);

            //Connect to the FrameStream pipe and receive a PipeEndpoint
            //PipeEndpoints a symmetric on client and service meaning that
            //you can send and receive on both ends
            Pipe<WebcamImage>.PipeEndpoint p = c.FrameStream.Connect(-1);
            //Add a callback for when a new pipe packet is received
            p.PacketReceivedEvent += new_frame;

            //Start the packets streaming.  If there is an exception ignore it.
            //Exceptions are passed transparently to the client/service.
            try
            {
                c.StartStreaming();
            }
            catch (Exception e)
            {
                Console.WriteLine("Was already streaming...");
            }
            
            //Show a named window
            CvInvoke.NamedWindow("Image");

            //Loop through and show the new image if available
            while (true)
            {
                if (current_frame != null)
                {
                    CvInvoke.Imshow("Image", current_frame);
                }
                //Break the loop if "enter" is pressed on a window
                if (CvInvoke.WaitKey(50) != -1)
                    break;

            }


            //Close the window
            CvInvoke.DestroyWindow("Image");
            
            //Close the PipeEndpoint
            p.Close();

            //Stop streaming frame
            c.StopStreaming();

            //Shutdown Robot Raconteur
            RobotRaconteurNode.s.Shutdown();
            
        }

        static Image<Bgr, byte> current_frame = null;

        //Convert a frame to OpenCV format
        static Image<Bgr, byte> WebcamImageToCVImage(WebcamImage i)
        {
            Image<Bgr, byte> o = new Image<Bgr, byte>(i.width, i.height);
            o.Bytes = i.data;
            return o;

        }

        //Function to handle when a new frame is received
        //This function will be called by a separate thread by
        //Robot Raconteur.
        //Note: callbacks don't always need to be static
        static void new_frame(Pipe<WebcamImage>.PipeEndpoint pipe_ep)
        {
            //Get the newest frame and save it to the variable to be shown
            //by the display thread
            while (pipe_ep.Available > 0)
            {
                WebcamImage image = pipe_ep.ReceivePacket();
                current_frame = WebcamImageToCVImage(image);
            }
        }

    }
}
