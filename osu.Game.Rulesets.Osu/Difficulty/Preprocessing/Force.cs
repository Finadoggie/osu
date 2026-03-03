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
        public float AssumedRadius { get; set; }

        public Vector2 StartPosition { get; set; }
        public Vector2 EndPosition { get; set; }

        public Force? NextForce { get; set; }
        public Force? PrevForce { get; set; }

        public Movement Parent { get; set; }

        public string AimType { get; set; }

        public Force() { }

        public double? Angle(Force? other = null, bool signed = false)
        {
            other ??= PrevForce;
            if (other is null) return 0;

            Vector2 v1 = EndPosition - other.EndPosition;
            Vector2 v2 = EndPosition - StartPosition;

            float dot = Vector2.Dot(v1, v2);
            float det = v1.X * v2.Y - v1.Y * v2.X;

            double angle = Math.Atan2(det, dot);
            return signed ? angle : Math.Abs(angle);
        }
    }
}
