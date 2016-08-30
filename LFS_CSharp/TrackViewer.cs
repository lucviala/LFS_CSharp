﻿using InSimDotNet.Out;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LFS_CSharp
{
    public partial class TrackViewer : Form
    {

        private Graphics g;
        private PointF[] posCar = new PointF[20];
        private int height;
        OutSim outsim;
        private PointF lastPosition;
        private int width;
        private Thread outsimThread;
        private PTH pth;
        private Pen penLimit;
        private Pen penRoad;
        
        [STAThread]
        static void Main2(object state)
        {
            Application.Run((Form)state);
        }

        public static TrackViewer Create()
        {
            TrackViewer form = new TrackViewer();
            Thread thread = new Thread(Main2);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(form);
            return form;
        }

        public TrackViewer()
        {
            InitializeComponent();
        }

        private void btn_open_track_Click(object sender, EventArgs e)
        {
            TrackParser trackParser = new TrackParser("BL2");
            if((pth = trackParser.ParsePTHTrack()) != null)
            {
                drawTrack(pth);
            }

        }

        public void drawTrack(PTH track)        
        {

            penLimit = new Pen(Color.Brown, 1.0f);
            penRoad = new Pen(Color.Orange, 1.0f);
            PointF[] points = new PointF[track.GetNumNodes()];
            PointF[] pointsDir = new PointF[track.GetNumNodes()];
            PointF[] pointsRoadLeft = new PointF[track.GetNumNodes()];
            PointF[] pointsRoadRight = new PointF[track.GetNumNodes()];
            PointF[] pointsLimitLeft = new PointF[track.GetNumNodes()];
            PointF[] pointsLimitRight = new PointF[track.GetNumNodes()];
            for (int i = 0; i < track.GetNumNodes(); i++)
            {
                pointsDir[i] = new PointF(track.GetPTHNodes()[i].GetDirY(), track.GetPTHNodes()[i].GetDirX());
                points[i] = new PointF(convertToFitPictureBox(width, track.GetPTHNodes()[i].GetCenterY(), track.minimumCenterY, track.maximumCenterY) +50, convertToFitPictureBox(height, track.GetPTHNodes()[i].GetCenterX(), track.minimumCenterX, track.maximumCenterX)+50);
                pointsRoadLeft[i] = computeDriveLeftPoint(points[i], track.GetPTHNodes()[i].GetDriveLeft(), pointsDir[i]);
                pointsRoadRight[i] = computeDriveRightPoint(points[i], track.GetPTHNodes()[i].GetDriveRight(), pointsDir[i]);
                pointsLimitLeft[i] = computeDriveLeftPoint(points[i], track.GetPTHNodes()[i].GetLimitLeft(), pointsDir[i]);
                pointsLimitRight[i] = computeDriveRightPoint(points[i], track.GetPTHNodes()[i].GetLimitRight(), pointsDir[i]);
            }

            for(int i = 0; i < points.Length-1; i++)
            {
                g.DrawLine(Pens.Green, pointsLimitLeft[i], pointsLimitLeft[i+1]);
                if (i == points.Length - 2) g.DrawLine(Pens.Green, pointsLimitLeft[i + 1], pointsLimitLeft[0]);
                g.DrawLine(Pens.Purple, pointsLimitRight[i], pointsLimitRight[i+1]);
                if (i == points.Length - 2) g.DrawLine(Pens.Purple, pointsLimitRight[i + 1], pointsLimitRight[0]);
                g.DrawLine(penLimit, pointsLimitLeft[i], pointsLimitRight[i]);
                if (i == points.Length - 2) g.DrawLine(penLimit, pointsLimitLeft[i + 1], pointsLimitRight[i + 1]);
                g.DrawLine(penRoad, pointsRoadLeft[i], pointsRoadRight[i]);
                if (i == points.Length - 2) g.DrawLine(penRoad, pointsRoadLeft[i + 1], pointsRoadRight[i+1]);
                g.DrawLine(Pens.Red, points[i], points[i+1]);
                if (i == points.Length - 2) g.DrawLine(Pens.Red, points[i + 1], points[0]);
                //g.DrawLine(Pens.Green, points[i - 1].X, points[i - 1].Y, points[i - 1].X + pointsDir[i - 1].X * 20, points[i - 1].Y + pointsDir[i - 1].Y * 20);
            }
            this.outsimThread = new Thread(new ThreadStart(CallOutsimThread));
            this.outsimThread.Start();
        }
        
        private PointF computeDriveLeftPoint(PointF center, float limitLeft, PointF dir)
        {
            PointF LimitLeftPoint = new PointF();
            LimitLeftPoint.X = (center.X - limitLeft * 0.5f * (float)Math.Cos(Math.Atan2(dir.X, dir.Y)));
            LimitLeftPoint.Y = (center.Y + limitLeft * 0.5f * (float)Math.Sin(Math.Atan2(dir.X, dir.Y)));
            return LimitLeftPoint;
        }

        private PointF computeDriveRightPoint(PointF center, float limitRight, PointF dir)
        {
            PointF LimitRightPoint = new PointF();
            LimitRightPoint.X = (center.X - limitRight * 0.5f * (float)Math.Cos(Math.Atan2(dir.X, dir.Y)));
            LimitRightPoint.Y = (center.Y + limitRight * 0.5f * (float)Math.Sin(Math.Atan2(dir.X, dir.Y)));
            return LimitRightPoint;
        }

        public float convertToFitPictureBox(int taille, int point, int minimum, int maximum)
        {
            int offset = -minimum;
            float retour = ((float)(taille-100)/(((float)maximum + (float)offset) - ((float)minimum + (float)offset)))* ((float)point+(float)offset);
            Console.WriteLine(retour);
            return retour;
        }

        private void TrackViewer_Paint(object sender, PaintEventArgs e)
        {
            g = pictureBox1.CreateGraphics();
            height = pictureBox1.Height;
            width = pictureBox1.Width;
        }

        private void CallOutsimThread()
        {
            try
            {
                Console.WriteLine("Outsim Thread Started!");
                outsim = new OutSim();
                outsim.PacketReceived += (sender, e) =>
                {
                    posCar[posCar.Length - 1] = new PointF(convertToFitPictureBox(width, (int)e.Pos.Y, pth.minimumCenterY, pth.maximumCenterY), convertToFitPictureBox(height, (int)e.Pos.X, pth.minimumCenterX, pth.maximumCenterX));
                    Array.Copy(posCar, 1, posCar, 0, posCar.Length - 1);
                    ShowOutsimValues(e.Pos.X, e.Pos.Y);

                };
                outsim.Connect("127.0.0.1", 29966);
            }
            catch(SocketException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        delegate void ShowOutsimValuesDelegate(double posX, double posY);
        private void ShowOutsimValues(double posX, double posY)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new ShowOutsimValuesDelegate(ShowOutsimValues), new object[] { posX, posY});
                }
                else
                {
                    updateCar();
                }
            }
            catch (ObjectDisposedException ex)
            {
                if (outsimThread.IsAlive)
                    outsimThread.Abort();
            }
            catch(InvalidAsynchronousStateException ex)
            {
                if (outsimThread.IsAlive)
                    outsimThread.Abort();
            }
        }

        public void updateCar()
        {
            if (lastPosition.X != 0 && lastPosition.Y != 0) {
                g.DrawLine(Pens.Blue, posCar[posCar.Length - 1].X + 50, posCar[posCar.Length - 1].Y+50, lastPosition.X+50, lastPosition.Y+50);
            }
            lastPosition = posCar[posCar.Length - 1];
        }

        private void TrackViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (outsimThread.IsAlive)
            {
                if (!outsim.IsConnected)
                {
                    outsim.Disconnect();
                    outsim = null;
                }
                outsimThread.Abort();
            }
        }
    }
}
