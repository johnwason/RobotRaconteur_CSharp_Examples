using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using experimental.createwebcam2;
using RobotRaconteur;
using System.Threading;
using System.Runtime.InteropServices;

namespace SimpleWebcamService
{
    //This program provides a simple Robot Raconteur server for viewing multiple webcams.
    //It uses the Webcam_interface.robdef service definition
    class Program
    {
        static void Main(string[] args)
        {            
            //Create a tuple list with the camera index/camera name and
            //then initalize the host, which in turn initializes the cameras
            Tuple<int, string>[] webcamnames = new Tuple<int, string>[] {new Tuple<int,string>(0,"Left"), new Tuple<int,string>(1,"Right") };
            WebcamHost_impl host = new WebcamHost_impl(webcamnames);

            // Use ServerNodeSetup to initialize server node
            using (new ServerNodeSetup("experimental.createwebcam2", 2355))
            {
                //Register the webcam host object as a service so that it can be connected to
                RobotRaconteurNode.s.RegisterService("Webcam", "experimental.createwebcam2", host);

                //Stay open until shut down
                Console.WriteLine("Webcam server started. Connect with URL rr+tcp://localhost:2355?service=Webcam Press enter to exit");
                Console.ReadLine();

                //Shutdown
                host.Shutdown();
            }
        }
    }


    //Class that implements the "WebcamHost" Robot Raconteur object type
    public class WebcamHost_impl : WebcamHost
    {

        Dictionary<int, Webcam_impl> webcams=new Dictionary<int,Webcam_impl>();

        //Initialize the webcams
        public WebcamHost_impl(Tuple<int, string>[] cameranames)
        {
            int camcount = 0;
            foreach (Tuple<int, string> c in cameranames)
            {
                Webcam_impl w = new Webcam_impl(c.Item1, c.Item2);
                webcams.Add(camcount, w);
                camcount++;

            }

        }
        
        //Return the indices and names of the available webcams
        public Dictionary<int, string> WebcamNames {
            get
            {
                lock (webcams)
                {
                    Dictionary<int, string> o = new Dictionary<int, string>();
                    foreach (KeyValuePair<int, Webcam_impl> w in webcams)
                    {
                        o.Add(w.Key, w.Value.Name);
                    }
                    return o;
                }
            }
            set
            {
                throw new InvalidOperationException("Read only property");
            }
        }

        //Function to implement the "Webcams" objref.  Return the
        //object for the selected webcam
        public Webcam get_Webcams(int ind)
        {
            lock (webcams)
            {
                return webcams[ind];
            }
        }

        //Shutdown all webcams
        public void Shutdown()
        {
            lock (webcams)
            {
                foreach (KeyValuePair<int, Webcam_impl> w in webcams)
                {
                    w.Value.Shutdown();
                }
            }
        }
    }

    //Class to implement the "Webcam" Robot Raconteur object
    public class Webcam_impl : Webcam_default_impl
    {

        VideoCapture _capture;

        //Initialize the webcam
        public Webcam_impl(int cameraid, string cameraname)
        {
            _capture = new VideoCapture(cameraid);
            _capture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, 320);
            _capture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, 240);
            _Name = cameraname;
        }

        //Shutdown the webcam
        public void Shutdown()
        {
            lock (this)
            {
                _capture.Dispose();
            }
        }

        string _Name="";
        
        //"Name" property
        public override string Name {
            get
            {
                return _Name;
            }            
        }

        //Function to capture a frame and return the Robot Raconteur WebcamImage structure
        public override WebcamImage CaptureFrame()
        {
            lock (this)
            {
                var i = _capture.QueryFrame();
                byte[] data = (byte[])i.GetRawData(); 

                WebcamImage o = new WebcamImage();
                o.height = i.Height;
                o.width = i.Width;
                o.step = i.Step;
                o.data = data;
                return o;
            }

        }

        bool streaming = false;

        //Start streaming frames
        public override void StartStreaming()
        {
            lock (this)
            {
                if (streaming) throw new InvalidOperationException("Already streaming");
                streaming = true;
                
                //Create a thread that retrieves and transmits frames
                Thread t = new Thread(frame_threadfunc);
                t.Start();
            }
        }

        //Stop the image streaming frame
        public override void StopStreaming()
        {
            lock (this)
            {
                if (!streaming) throw new InvalidOperationException("Not streaming");
                streaming = false;
            }
        }

        //Thread to stream frames by capturing data and sending it to
        //all connected PipeEndpoints
        public void frame_threadfunc()
        {
            while (streaming)
            {
                //Capture a frame
                WebcamImage frame = CaptureFrame();

                if (rrvar_FrameStream != null)
                {
                    rrvar_FrameStream.SendPacket(frame);
                }
               
                Thread.Sleep(100);
            }

        }

        //Override FrameStream to set MaximumBacklog
        public override Pipe<WebcamImage> FrameStream
        {
            get
            {
                return base.FrameStream;
            }
            set
            {
                base.FrameStream = value;
                rrvar_FrameStream.MaximumBacklog = 3;
            }
        }

        byte[] _buffer = new byte[0];
        MultiDimArray _multidimbuffer = new MultiDimArray(new uint[3] {0,0,0}, new byte[0]);

        //Capture a frame and save it to the memory buffers
        public override WebcamImage_size CaptureFrameToBuffer()
        {
            WebcamImage image = CaptureFrame();
            _buffer = image.data;

            //Rearrange the data into the correct format for MATLAB arrays
            byte[] mdata=new byte[image.height*image.width*3];
            MultiDimArray mdbuf = new MultiDimArray(new uint[] {(uint)image.height, (uint)image.width, 3 }, mdata);
            for (int channel=0; channel < 3; channel++)
            {
                int channel0 = image.height * image.width * channel;
                for (int x = 0; x < image.width; x++)
                {                        
                    for (int y = 0; y < image.height; y++)
                    {
                        byte value = image.data[(y * image.step + x*3)  + (2-channel)];
                        mdata[channel0 + x * image.height + y]=value;
                    }
                }
            }
            _multidimbuffer=mdbuf;

            //Return a WebcamImage_size structure to the client
            WebcamImage_size size = new WebcamImage_size();
            size.width = image.width;
            size.height = image.height;
            size.step = image.step;
            return size;
        }

        //Return an ArrayMemory for the "buffer" data containing the image.
        public override ArrayMemory<byte> buffer {
            get
            {
                //In many cases this ArrayMemory would not be initialized every time,
                //but for this example return a new ArrayMemory
                return new ArrayMemory<byte>(_buffer);
            }
           
        }

        //Return a MultiDimArray for the "multidimbuffer" data containing the image
        public override MultiDimArrayMemory<byte> multidimbuffer {
            get
            {
                //In many cases this MultiDimArrayMemory would not be initialized every time,
                //but for this example return a new MultiDimArrayMemory
                return new MultiDimArrayMemory<byte>(_multidimbuffer);
            }            
        }
    }
}
