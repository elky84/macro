﻿using IronPython.Runtime;
using KPUGeneralMacro.Extension;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace KPUGeneralMacro.Model
{
    public class ExtensionColor
    {
        public bool Activated { get; set; }
        public bool DetectColor { get; set; }
        public Color Pivot { get; set; }
        public float Factor { get; set; }
    }

    public class Sprite
    {
        public struct DetectionResult
        { 
            public OpenCvSharp.Rect Rect { get; set; }
            public OpenCvSharp.Point Position { get; set; }
            public double Percentage { get; set; }

            public PythonDictionary ToPythonDictionary()
            {
                return new PythonDictionary
                { 
                    ["rect"] = new PythonDictionary
                    { 
                        ["x"] = Rect.X,
                        ["y"] = Rect.Y,
                        ["width"] = Rect.Width,
                        ["height"] = Rect.Height
                    },
                    ["position"] = new PythonTuple(new object[] { Position.X, Position.Y }),
                    ["percent"] = Percentage
                };
            }
        }

        public string Name { get; set; }
        public Mat Mat { get; set; }
        public ExtensionColor ExtensionColor { get; set; }
        public float Threshold { get; set; } = 0.8f;

        public DetectionResult MatchTo(Mat frame, OpenCvSharp.Rect? area = null)
        {
            if (frame == null)
                throw new Exception("frame cannot be null");

            if (area != null)
                frame = new Mat(frame, area.Value).Clone();

            var from = this.ExtensionColor.Activated ?
                frame.ToMask(this.ExtensionColor.Pivot, this.ExtensionColor.Factor) : frame.Clone();
            var to = this.ExtensionColor.Activated ?
                this.Mat.ToMask(this.ExtensionColor.Pivot, this.ExtensionColor.Factor) : this.Mat.Clone();

            try
            {
                var result = new DetectionResult();
                if (ExtensionColor.Activated && ExtensionColor.DetectColor)
                {
                    var detectedRect = from.GetRotatedRects(Threshold).OrderByDescending(x => x.Size.Width * x.Size.Height).FirstOrDefault();
                    result = new DetectionResult
                    {
                        Rect = new Rect((int)(detectedRect.Center.X - detectedRect.Size.Width / 2.0),
                                        (int)(detectedRect.Center.Y - detectedRect.Size.Height / 2.0),
                                        (int)(detectedRect.Center.X + detectedRect.Size.Width / 2.0),
                                        (int)(detectedRect.Center.Y + detectedRect.Size.Height / 2.0)),
                        Position = (OpenCvSharp.Point)detectedRect.Center,
                        Percentage = 1.0
                    };
                }
                else
                {
                    var matched = from.MatchTemplate(to, TemplateMatchModes.CCoeffNormed);
                    matched.MinMaxLoc(out var minval, out var maxval, out var minloc, out var maxloc);

                    var percentage = maxval;
                    var center = new OpenCvSharp.Point(maxloc.X + this.Mat.Width / 2, maxloc.Y + this.Mat.Height / 2);

                    result = new DetectionResult
                    {
                        Rect = new Rect(maxloc.X, maxloc.Y, this.Mat.Width, this.Mat.Width),
                        Position = center,
                        Percentage = percentage
                    };

                    matched.Dispose();
                }
                

                if (area != null)
                {
                    result.Rect = new Rect(result.Rect.X + area.Value.X, result.Rect.Y + area.Value.Y, result.Rect.Width, result.Rect.Height);

                    result.Position = new OpenCvSharp.Point
                    {
                        X = result.Position.X + area.Value.X,
                        Y = result.Position.Y + area.Value.Y,
                    };
                }

                return result;
            }
            catch(Exception)
            {
                return new DetectionResult();
            }
            finally
            {
                from.Dispose();
                to.Dispose();
            }
        }

        public List<DetectionResult> MatchToAll(Mat frame, float percent, OpenCvSharp.Rect? area = null)
        {
            var result = new List<DetectionResult>();
            frame = frame.Clone();
            while (true)
            {
                var detectionResult = MatchTo(frame, area);
                if (detectionResult.Percentage < percent)
                    break;

                var roi = new Mat(frame, detectionResult.Rect);
                roi.SetTo(Scalar.Black);

                result.Add(detectionResult);
            }

            return result;
        }
    }
}
