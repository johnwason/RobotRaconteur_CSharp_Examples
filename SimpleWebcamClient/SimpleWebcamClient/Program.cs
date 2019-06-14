using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RobotRaconteur;
using experimental.createwebcam2;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;

namespace SimpleWebcamClient
{
    //Simple client to read images from a Webcam server
    //and display the images

    class Program
    {
        static void Main(string[] args)
        {
            //Use ClientNodeSetup to initialize node
            using (new ClientNodeSetup())
            {

                //Connect to the service
                WebcamHost c_host = (WebcamHost)RobotRaconteurNode.s.ConnectService("rr+tcp://localhost:2355?service=Webcam", objecttype: "experimental.createwebcam2.WebcamHost");

                //Get the Webcam objects from the "Webcams" objref
                Webcam c1 = c_host.get_Webcams(0);
                Webcam c2 = c_host.get_Webcams(1);

                //Capture an image and convert to OpenCV image type
                Image<Bgr, byte> frame1 = WebcamImageToCVImage(c1.CaptureFrame());
                Image<Bgr, byte> frame2 = WebcamImageToCVImage(c1.CaptureFrame());

                //Show image
                CvInvoke.Imshow(c1.Name, frame1);
                CvInvoke.Imshow(c2.Name, frame2);

                //Wait for enter to be pressed
                CvInvoke.WaitKey(0);
            }
        }

        //Convert WebcamImage to OpenCV format
        static Image<Bgr, byte> WebcamImageToCVImage(WebcamImage i)
        {
            Image<Bgr, byte> o = new Image<Bgr, byte>(i.width, i.height);
            o.Bytes = i.data;
            return o;

        }
    }
}
