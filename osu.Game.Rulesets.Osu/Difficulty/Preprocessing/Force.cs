// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    /// <summary>
    /// A force to be applied to the cursor, assuming the cursor has a mass of 1
    /// </summary>
    public class Force
    {
        public double Acceleration { get; set; }
        public double ForceDuration { get; set; }
        public double AbsoluteAngle { get; set; }
        public double StartTime { get; set; }
        public double StartVelocity { get; set; } = 0;
        public double StartVelocityAngle { get; set; } = 0;
        public double EndVelocity { get; set; } = 0;
        public double EndVelocityAngle { get; set; } = 0;
        public bool EndsInClick { get; set; } = false;
        public float ScalingFactor { get; set; }

        public Vector2 StartPosition { get; set; }
        public Vector2 EndPosition { get; set; }

        public Force? NextForce { get; set; }
        public Force? PrevForce { get; set; }

        public Force() { }

        public double? Angle()
        {
            if (PrevForce == null) return null;

            double angle = Math.Abs(AbsoluteAngle - PrevForce.AbsoluteAngle);
            if (angle > double.DegreesToRadians(180)) angle -= 2 * Math.PI;

            return angle;
        }
    }
}
