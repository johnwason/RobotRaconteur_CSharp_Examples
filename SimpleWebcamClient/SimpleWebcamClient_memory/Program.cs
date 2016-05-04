using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.createwebcam;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;

namespace SimpleWebcamClient_memory
{
    //Simple client to read images from a Webcam server
    //and display the image.  This example uses the "memory"
    //member type
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

            //Get the Webcam objects from the "Webcams" objref
            Webcam c1 = c_host.get_Webcams(0);

            //Capture an image to the "buffer" and "multidimbuffer"
            WebcamImage_size size=c1.CaptureFrameToBuffer();

            //Read the full image from the "buffer" memory
            ulong l = c1.buffer.Length;
            byte[] data = new byte[l];
            c1.buffer.Read(0, data, 0, l);

            //Convert and show the image retrieved from the buffer memory
            Image<Bgr, byte> frame1 = new Image<Bgr, byte>(size.width, size.height);
            frame1.Bytes = data;
            CvInvoke.Imshow("buffer", frame1);
            
            //Read the dimensions of the "multidimbuffer" member
            ulong[] bufsize=c1.multidimbuffer.Dimensions;
           
            //Retrieve the data from the "multidimbuffer"
            byte[] segdata_bytes = new byte[100000];
            MultiDimArray segdata = new MultiDimArray(new int[] { 100, 100, 1 }, segdata_bytes);
            c1.multidimbuffer.Read(new ulong[] { 10, 10, 0 }, segdata, new ulong[] { 0, 0, 0 }, new ulong[] { 100, 100, 1 });

            //Create a new image to hold the image
            Image<Gray, byte> frame2 = new Image<Gray, byte>(100,100);

            //This will actually give you the transpose of the image because MultiDimArray is stored in column-major order,
            //as an exercise transpose the image to be the correct orientation
            frame2.Bytes = segdata_bytes;

            //Rotate and flip the image to get the right orientation
            Image<Gray, byte> frame3 = frame2.Rotate(90, new Gray(0));
            Image<Gray, byte> frame4 = frame3.Flip(Emgu.CV.CvEnum.FlipType.Horizontal);

            //Show the image
            CvInvoke.Imshow("multidimbuffer", frame4);
            CvInvoke.WaitKey(0);

            //Shutdown Robot Raconteur
            RobotRaconteurNode.s.Shutdown();



        }
    }
}
