﻿// Ours
//------------------------------------------------------------------------------
// <copyright file="FallingThings.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

// This module contains code to do display falling shapes, and do
// hit testing against a set of segments provided by the Kinect NUI, and
// have shapes react accordingly.

namespace ShapeGame
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using Microsoft.Kinect;
    using ShapeGame.Utils;

    // FallingThings is the main class to draw and maintain positions of falling shapes.  It also does hit testing
    // and appropriate bouncing.
    public class FallingThings
    {
        private const double BaseGravity = 0.017;
        private const double BaseAirFriction = 0.994;

        private readonly Dictionary<PolyType, PolyDef> polyDefs = new Dictionary<PolyType, PolyDef>
            {
                { PolyType.Triangle, new PolyDef { Sides = 3, Skip = 1 } },
                { PolyType.Star, new PolyDef { Sides = 5, Skip = 2 } },
                { PolyType.Pentagon, new PolyDef { Sides = 5, Skip = 1 } },
                { PolyType.Square, new PolyDef { Sides = 4, Skip = 1 } },
                { PolyType.Hex, new PolyDef { Sides = 6, Skip = 1 } },
                { PolyType.Star7, new PolyDef { Sides = 7, Skip = 3 } },
                { PolyType.Circle, new PolyDef { Sides = 1, Skip = 1 } },
                { PolyType.Bubble, new PolyDef { Sides = 0, Skip = 1 } }
            };

        private readonly List<Thing> things = new List<Thing>();
        private readonly Random rnd = new Random();
        private readonly List<int> values = new List<int>();
        private const int maxValue = 20;
        private readonly int maxThings;
        private readonly int intraFrames = 1;
        private readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        private const double DissolveTime = 0.4;
        private Rect sceneRect;
        private double targetFrameRate = 60;
        private double dropRate = 2.0;
        private double shapeSize = 1.0;
        private double baseShapeSize = 20;
        private GameMode gameMode = GameMode.Off;
        private double gravity = BaseGravity;
        private double gravityFactor = 1.0;
        private double airFriction = BaseAirFriction;
        private int frameCount;
        private bool doRandomColors = true;
        private double expandingRate = 1.0;
        private System.Windows.Media.Color baseColor = System.Windows.Media.Color.FromRgb(0, 0, 0);
        private PolyType polyTypes = PolyType.All;
        private DateTime gameStartTime;
        private int target = -1;

        public FallingThings(int maxThings, double framerate, int intraFrames)
        {
            this.maxThings = maxThings;
            this.intraFrames = intraFrames;
            this.targetFrameRate = framerate * intraFrames;
            this.SetGravity(this.gravityFactor);
            this.sceneRect.X = this.sceneRect.Y = 0;
            this.sceneRect.Width = this.sceneRect.Height = 100;
            this.shapeSize = this.sceneRect.Height * this.baseShapeSize / 1000.0;
            this.expandingRate = Math.Exp(Math.Log(6.0) / (this.targetFrameRate * DissolveTime));
        }

        public enum ThingState
        {
            Falling = 0,
            Bouncing = 1,
            Dissolving = 2,
            Remove = 3,
            Held = 4
        }

        public static Label MakeSimpleLabel(string text, Rect bounds, System.Windows.Media.Brush brush)
        {
            Label label = new Label { Content = text };
            if (bounds.Width != 0)
            {
                label.SetValue(Canvas.LeftProperty, bounds.Left);
                label.SetValue(Canvas.TopProperty, bounds.Top);
                label.Width = bounds.Width;
                label.Height = bounds.Height;
            }

            label.Foreground = brush;
            label.FontFamily = new System.Windows.Media.FontFamily("Arial");
            label.FontWeight = FontWeight.FromOpenTypeWeight(600);
            label.FontStyle = FontStyles.Normal;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        public void SetFramerate(double actualFramerate)
        {
            this.targetFrameRate = actualFramerate * this.intraFrames;
            this.expandingRate = Math.Exp(Math.Log(6.0) / (this.targetFrameRate * DissolveTime));
            if (this.gravityFactor != 0)
            {
                this.SetGravity(this.gravityFactor);
            }
        }

        public void SetBoundaries(Rect r)
        {
            this.sceneRect = r;
            this.shapeSize = r.Height * this.baseShapeSize / 1000.0;
        }

        public void SetDropRate(double f)
        {
            this.dropRate = f;
        }

        public void SetSize(double f)
        {
            this.baseShapeSize = f;
            this.shapeSize = this.sceneRect.Height * this.baseShapeSize / 1000.0;
        }

        public void SetShapesColor(System.Windows.Media.Color color, bool doRandom)
        {
            this.doRandomColors = doRandom;
            this.baseColor = color;
        }

        public void Reset()
        {
            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                if ((thing.State == ThingState.Bouncing) || (thing.State == ThingState.Falling))
                {
                    thing.State = ThingState.Dissolving;
                    thing.Dissolve = 0;
                    this.things[i] = thing;
                }
            }

            this.gameStartTime = DateTime.Now;
            this.scores.Clear();
        }

        public void SetGameMode(GameMode mode)
        {
            this.gameMode = mode;
            this.gameStartTime = DateTime.Now;
            this.scores.Clear();
        }

        public void SetGravity(double f)
        {
            this.gravityFactor = f;
            this.gravity = f * BaseGravity / this.targetFrameRate / Math.Sqrt(this.targetFrameRate) / Math.Sqrt(this.intraFrames);
            this.airFriction = f == 0 ? 0.997 : Math.Exp(Math.Log(1.0 - ((1.0 - BaseAirFriction) / f)) / this.intraFrames);

            if (f == 0)
            {
                // Stop all movement as well!
                for (int i = 0; i < this.things.Count; i++)
                {
                    Thing thing = this.things[i];
                    thing.XVelocity = thing.YVelocity = 0;
                    this.things[i] = thing;
                }
            }
        }
        public void SetPolies(PolyType polies)
        {
            this.polyTypes = polies;
        }
        public HitType LookForHits(Dictionary<Bone, BoneData> segments, Player player)
        {
            DateTime cur = DateTime.Now;
            HitType allHits = HitType.None;

            // Zero out score if necessary
            if (!this.scores.ContainsKey(player.GetId()))
            {
                this.scores.Add(player.GetId(), 0);
            }

            foreach (var pair in segments)
            {
                if (pair.Key.Joint1 != JointType.HandLeft
                    && pair.Key.Joint1 != JointType.HandRight
                    && pair.Key.Joint1 != JointType.FootLeft
                    && pair.Key.Joint1 != JointType.FootRight)
                {
                    continue;
                }

                var hitCenter = new System.Windows.Point(0, 0);
                double lineHitLocation = 0;
                Segment seg = pair.Value.GetEstimatedSegment(cur);

                if (player.Holding.ContainsKey(pair.Key.Joint1))
                {
                    Thing heldThing = (Thing)player.Holding[pair.Key.Joint1];
                    if (heldThing.Hit(seg, ref hitCenter, ref lineHitLocation))
                    {
                        continue;
                    }
                    else
                    {
                        player.Holding.Remove(pair.Key.Joint1);
                        heldThing.State = ThingState.Falling;
                    }
                }

                if (player.Holding.Count == 3)
                {
                    continue;
                }

                for (int i = 0; i < this.things.Count; i++)
                {
                    Thing thing = this.things[i];

                    if ((thing.ValueType == StoredValueType.Number && player.Holding.countOfNumberThings() >= 2)
                        || thing.ValueType != StoredValueType.Number && player.Holding.countOfOperatorThings() >= 1
                        || player.Holding.ContainsValue(thing))
                    {
                        continue;
                    }

                    if (thing.Hit(seg, ref hitCenter, ref lineHitLocation))
                    {
                        double fMs = 1000;
                        if (thing.TimeLastHit != DateTime.MinValue)
                        {
                            fMs = cur.Subtract(thing.TimeLastHit).TotalMilliseconds;
                            thing.AvgTimeBetweenHits = (thing.AvgTimeBetweenHits * 0.8) + (0.2 * fMs);
                        }
                        thing.TimeLastHit = cur;
                        thing.TouchedBy = player.GetId();
                        player.Holding[pair.Key.Joint1] = thing;
                        thing.State = ThingState.Held;
                        break;
                    }
                }
            }

            return allHits;
        }

        public void AdvanceFrame()
        {
            // Move all things by one step, accounting for gravity
            for (int thingIndex = 0; thingIndex < this.things.Count; thingIndex++)
            {
                Thing thing = this.things[thingIndex];
                thing.Theta += thing.SpinRate;
                if (thing.State != ThingState.Held)
                {
                    thing.Center.Offset(thing.XVelocity, thing.YVelocity);
                }
                if (thing.Center.X - thing.Size < 0) //Left??
                {
                    thing.ObjectDirection = Direction.Left;
                    thing.XVelocity = -thing.XVelocity;
                    thing.Center.X += thing.XVelocity;
                }
				
                if(thing.Center.X + thing.Size > this.sceneRect.Width)//RIGHT?
                {
                    thing.ObjectDirection = Direction.Right;
                    thing.XVelocity = -thing.XVelocity;
                    thing.Center.X += thing.XVelocity;
                }

                if (thing.Center.Y - thing.Size > this.sceneRect.Bottom-100)
                {
                    thing.ObjectDirection = Direction.Up;
                    thing.YVelocity = -thing.YVelocity;
                    thing.Center.Y += thing.YVelocity;
                }

                if (thing.Center.Y + thing.Size < this.sceneRect.Top+100 && thing.ObjectDirection != Direction.Down)
                {
                    thing.ObjectDirection = Direction.Down;
                    thing.YVelocity = -thing.YVelocity;
                    thing.Center.Y += thing.YVelocity;
                }
                
                // Get rid of after dissolving.
                if (thing.State == ThingState.Dissolving)
                {
                    thing.Dissolve += 1 / (this.targetFrameRate * DissolveTime);
                    thing.Size *= this.expandingRate;
                    if (thing.Dissolve >= 1.0)
                    {
                        thing.State = ThingState.Remove;
                    }
                }

                this.things[thingIndex] = thing;
            }

            // Then remove any that should go away now
            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                if (thing.State == ThingState.Remove)
                {
                    this.things.Remove(thing);
                    i--;
                }
            }

            // Create any new things to drop based on dropRate
            while (this.values.Count < this.maxThings)
            {
                target = -1;

                int randVal = this.rnd.Next(maxValue);
                values.Add(randVal);

                PolyType[] alltypes = 
                {
                    PolyType.Triangle, PolyType.Square, PolyType.Star, PolyType.Pentagon,
                    PolyType.Hex, PolyType.Star7, PolyType.Circle, PolyType.Bubble
                };
                byte r;
                byte g;
                byte b;

                if (this.doRandomColors)
                {
                    r = (byte)(this.rnd.Next(215) + 40);
                    g = (byte)(this.rnd.Next(215) + 40);
                    b = (byte)(this.rnd.Next(215) + 40);
                }
                else
                {
                    r = (byte)Math.Min(255.0, this.baseColor.R * (0.7 + (this.rnd.NextDouble() * 0.7)));
                    g = (byte)Math.Min(255.0, this.baseColor.G * (0.7 + (this.rnd.NextDouble() * 0.7)));
                    b = (byte)Math.Min(255.0, this.baseColor.B * (0.7 + (this.rnd.NextDouble() * 0.7)));
                }

                PolyType tryType;
                do
                {
                    tryType = alltypes[this.rnd.Next(alltypes.Length)];
                }
                while ((this.polyTypes & tryType) == 0);

                this.DropNewThing(tryType, this.shapeSize, System.Windows.Media.Color.FromRgb(r, g, b), StoredValueType.Number, randVal);
            }

            if (target == -1)
            {
                target = Numbers.GenerateTargetNumber(values);
            }
        }

        public void DrawFrame(UIElementCollection children, Dictionary<int, Player> players)
        {
            this.frameCount++;

            // Draw all shapes in the scene
            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                if (thing.Brush == null)
                {
                    thing.Brush = new SolidColorBrush(thing.Color);
                    double factor = 0.4 + (((double)thing.Color.R + thing.Color.G + thing.Color.B) / 1600);
                    thing.Brush2 =
                        new SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                (byte)(255 - ((255 - thing.Color.R) * factor)),
                                (byte)(255 - ((255 - thing.Color.G) * factor)),
                                (byte)(255 - ((255 - thing.Color.B) * factor))));
                    thing.BrushPulse = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                }

                if (thing.State == ThingState.Bouncing)
                {
                    // Pulsate edges
                    double alpha = Math.Cos((0.15 * (thing.FlashCount++) * thing.Hotness) * 0.5) + 0.5;

                    children.Add(
                        this.MakeSimpleShape(
                            this.polyDefs[thing.Shape].Sides,
                            this.polyDefs[thing.Shape].Skip,
                            thing.Size,
                            thing.Theta,
                            thing.Center,
                            thing.Brush,
                            thing.BrushPulse,
                            thing.Size * 0.1,
                            alpha));
                    this.things[i] = thing;
                }
                else
                {
                    if (thing.State == ThingState.Dissolving)
                    {
                        thing.Brush.Opacity = 1.0 - (thing.Dissolve * thing.Dissolve);
                    }

                    children.Add(
                        this.MakeSimpleShape(
                            this.polyDefs[thing.Shape].Sides,
                            this.polyDefs[thing.Shape].Skip,
                            thing.Size,
                            thing.Theta,
                            thing.Center,
                            thing.Brush,
                            (thing.State == ThingState.Dissolving) ? null : thing.Brush2,
                            1,
                            1));
                }
            }

            // Show scores
            if (this.scores.Count != 0)
            {
                int i = 0;
                foreach (var score in this.scores)
                {
                    Label label = MakeSimpleLabel(
                        score.Value.ToString(CultureInfo.InvariantCulture),
                        new Rect(
                            (0.02 + (i * 0.6)) * this.sceneRect.Width,
                            0.01 * this.sceneRect.Height,
                            0.4 * this.sceneRect.Width,
                            0.3 * this.sceneRect.Height), 
                            new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)));
                    label.FontSize = Math.Max(1, Math.Min(this.sceneRect.Width / 12, this.sceneRect.Height / 12));
                    children.Add(label);
                    i++;
                }
            }

            // Show target
            Label targetLabel = MakeSimpleLabel(
                    target.ToString(CultureInfo.InvariantCulture),
                    new Rect(
                        this.sceneRect.Width / 2,
                        0,
                        0.4 * this.sceneRect.Width,
                        0.3 * this.sceneRect.Height),
                        new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)));
            targetLabel.FontSize = Math.Max(1, Math.Min(this.sceneRect.Width / 12, this.sceneRect.Height / 12));
            children.Add(targetLabel);

            // Show equation
            string equationText = "";
            foreach (Player player in players.Values)
            {
                equationText += player.Holding.GetEquationString();
                equationText += "\n";
            }

            Label equationLabel = MakeSimpleLabel(
                    equationText,
                    new Rect(
                        this.sceneRect.Width / 2,
                        100,
                        0.4 * this.sceneRect.Width,
                        0.3 * this.sceneRect.Height),
                        new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)));
            equationLabel.FontSize = Math.Max(1, Math.Min(this.sceneRect.Width / 12, this.sceneRect.Height / 12));
            children.Add(equationLabel);

            // Show game timer
            if (this.gameMode != GameMode.Off)
            {
                TimeSpan span = DateTime.Now.Subtract(this.gameStartTime);
                string text = span.Minutes.ToString(CultureInfo.InvariantCulture) + ":" + span.Seconds.ToString("00");

                Label timeText = MakeSimpleLabel(
                    text,
                    new Rect(
                        0.1 * this.sceneRect.Width, 0.25 * this.sceneRect.Height, 0.89 * this.sceneRect.Width, 0.72 * this.sceneRect.Height),
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 255, 255)));
                timeText.FontSize = Math.Max(1, this.sceneRect.Height / 16);
                timeText.HorizontalContentAlignment = HorizontalAlignment.Right;
                timeText.VerticalContentAlignment = VerticalAlignment.Bottom;
                children.Add(timeText);
            }
        }

        private static double SquaredDistance(double x1, double y1, double x2, double y2)
        {
            return ((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1));
        }

        private void AddToScore(int player, int points, System.Windows.Point center)
        {
            if (this.scores.ContainsKey(player))
            {
                this.scores[player] = this.scores[player] + points;
            }
            else
            {
                this.scores.Add(player, points);
            }

            FlyingText.NewFlyingText(this.sceneRect.Width / 300, center, "+" + points);
        }

        public void DropNewThing(PolyType newShape, double newSize, System.Windows.Media.Color newColor, StoredValueType newValueType, int newValue = 0)
        {
            // Only drop within the center "square" area 
            double dropWidth = this.sceneRect.Bottom - this.sceneRect.Top;
            if (dropWidth > this.sceneRect.Right - this.sceneRect.Left)
            {
                dropWidth = this.sceneRect.Right - this.sceneRect.Left;
            }

            var newThing = new Thing
            {
                Size = newSize,
                YVelocity = ((0.5 * this.rnd.NextDouble()) - 0.25),
                XVelocity = ((0.5 * this.rnd.NextDouble()) - 0.25),
                Shape = newShape,
                Center = new System.Windows.Point(this.rnd.Next((int)this.sceneRect.Width), this.rnd.Next((int)this.sceneRect.Height)),
                SpinRate = ((this.rnd.NextDouble() * 12.0) - 6.0) * 2.0 * Math.PI / this.targetFrameRate / 4.0,
                Theta = 0,
                TimeLastHit = DateTime.MinValue,
                AvgTimeBetweenHits = 100,
                Color = newColor,
                Brush = null,
                Brush2 = null,
                BrushPulse = null,
                Dissolve = 0,
                State = ThingState.Falling,
                TouchedBy = 0,
                Hotness = 0,
                FlashCount = 0,
                ValueType = newValueType,
                Value = newValue
            };

            this.things.Add(newThing);
        }

        private Shape MakeSimpleShape(
            int numSides,
            int skip,
            double size,
            double spin,
            System.Windows.Point center,
            System.Windows.Media.Brush brush,
            System.Windows.Media.Brush brushStroke,
            double strokeThickness,
            double opacity)
        {
            if (numSides <= 1)
            {
                var circle = new Ellipse { Width = size * 2, Height = size * 2, Stroke = brushStroke };
                if (circle.Stroke != null)
                {
                    circle.Stroke.Opacity = opacity;
                }

                circle.StrokeThickness = strokeThickness * ((numSides == 1) ? 1 : 2);
                circle.Fill = (numSides == 1) ? brush : null;
                circle.SetValue(Canvas.LeftProperty, center.X - size);
                circle.SetValue(Canvas.TopProperty, center.Y - size);
                return circle;
            }

            var points = new PointCollection(numSides + 2);
            double theta = spin;
            for (int i = 0; i <= numSides + 1; ++i)
            {
                points.Add(new System.Windows.Point((Math.Cos(theta) * size) + center.X, (Math.Sin(theta) * size) + center.Y));
                theta = theta + (2.0 * Math.PI * skip / numSides);
            }

            var polyline = new Polyline { Points = points, Stroke = brushStroke };
            if (polyline.Stroke != null)
            {
                polyline.Stroke.Opacity = opacity;
            }

            polyline.Fill = brush;
            polyline.FillRule = FillRule.Nonzero;
            polyline.StrokeThickness = strokeThickness;
            return polyline;
        }

        internal struct PolyDef
        {
            public int Sides;
            public int Skip;
        }

        // The Thing struct represents a single object that is flying through the air, and
        // all of its properties.
        public class Thing
        {
            public System.Windows.Point Center;
            public double Size;
            public double Theta;
            public double SpinRate;
            public double YVelocity;
            public double XVelocity;
			public Direction ObjectDirection;
            public PolyType Shape;
            public System.Windows.Media.Color Color;
            public System.Windows.Media.Brush Brush;
            public System.Windows.Media.Brush Brush2;
            public System.Windows.Media.Brush BrushPulse;
            public double Dissolve;
            public ThingState State;
            public DateTime TimeLastHit;
            public double AvgTimeBetweenHits;
            public int TouchedBy;               // Last player to touch this thing
            public int Hotness;                 // Score level
            public int FlashCount;
            public StoredValueType ValueType;
            public int Value;

            // Hit testing between this thing and a single segment.  If hit, the center point on
            // the segment being hit is returned, along with the spot on the line from 0 to 1 if
            // a line segment was hit.
            public bool Hit(Segment seg, ref System.Windows.Point hitCenter, ref double lineHitLocation)
            {
                double minDxSquared = this.Size + seg.Radius;
                minDxSquared *= minDxSquared;

                // See if falling thing hit this body segment
                if (seg.IsCircle())
                {
                    if (SquaredDistance(this.Center.X, this.Center.Y, seg.X1, seg.Y1) <= minDxSquared)
                    {
                        hitCenter.X = seg.X1;
                        hitCenter.Y = seg.Y1;
                        lineHitLocation = 0;
                        return true;
                    }
                }
                else
                {
                    double sqrLineSize = SquaredDistance(seg.X1, seg.Y1, seg.X2, seg.Y2);
                    if (sqrLineSize < 0.5)
                    {
                        // if less than 1/2 pixel apart, just check dx to an endpoint
                        return SquaredDistance(this.Center.X, this.Center.Y, seg.X1, seg.Y1) < minDxSquared;
                    }

                    // Find dx from center to line
                    double u = ((this.Center.X - seg.X1) * (seg.X2 - seg.X1)) + (((this.Center.Y - seg.Y1) * (seg.Y2 - seg.Y1)) / sqrLineSize);
                    if ((u >= 0) && (u <= 1.0))
                    {   // Tangent within line endpoints, see if we're close enough
                        double intersectX = seg.X1 + ((seg.X2 - seg.X1) * u);
                        double intersectY = seg.Y1 + ((seg.Y2 - seg.Y1) * u);

                        if (SquaredDistance(this.Center.X, this.Center.Y, intersectX, intersectY) < minDxSquared)
                        {
                            lineHitLocation = u;
                            hitCenter.X = intersectX;
                            hitCenter.Y = intersectY;
                            return true;
                        }
                    }
                    else
                    {
                        // See how close we are to an endpoint
                        if (u < 0)
                        {
                            if (SquaredDistance(this.Center.X, this.Center.Y, seg.X1, seg.Y1) < minDxSquared)
                            {
                                lineHitLocation = 0;
                                hitCenter.X = seg.X1;
                                hitCenter.Y = seg.Y1;
                                return true;
                            }
                        }
                        else
                        {
                            if (SquaredDistance(this.Center.X, this.Center.Y, seg.X2, seg.Y2) < minDxSquared)
                            {
                                lineHitLocation = 1;
                                hitCenter.X = seg.X2;
                                hitCenter.Y = seg.Y2;
                                return true;
                            }
                        }
                    }

                    return false;
                }

                return false;
            }

            // Change our velocity based on the object's velocity, our velocity, and where we hit.
            public void BounceOff(double x1, double y1, double otherSize, double fXv, double fYv)
            {
                double x0 = this.Center.X;
                double y0 = this.Center.Y;
                double xv0 = this.XVelocity - fXv;
                double yv0 = this.YVelocity - fYv;
                double dist = otherSize + this.Size;
                double dx = Math.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0)));
                double xdif = x1 - x0;
                double ydif = y1 - y0;
                double newvx1 = 0;
                double newvy1 = 0;

                x0 = x1 - (xdif / dx * dist);
                y0 = y1 - (ydif / dx * dist);
                xdif = x1 - x0;
                ydif = y1 - y0;

                double bsq = dist * dist;
                double b = dist;
                double asq = (xv0 * xv0) + (yv0 * yv0);
                double a = Math.Sqrt(asq);
                if (a > 0.000001)
                {
                    // if moving much at all...
                    double cx = x0 + xv0;
                    double cy = y0 + yv0;
                    double csq = ((x1 - cx) * (x1 - cx)) + ((y1 - cy) * (y1 - cy));
                    double tt = asq + bsq - csq;
                    double bb = 2 * a * b;
                    double power = a * (tt / bb);
                    newvx1 -= 2 * (xdif / dist * power);
                    newvy1 -= 2 * (ydif / dist * power);
                }

                this.XVelocity += newvx1;
                this.YVelocity += newvy1;
                this.Center.X = x0;
                this.Center.Y = y0;
            }
        }
    }
}
