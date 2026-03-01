// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class Movement
    {
        public Vector2 Start { get; set; }
        public double StartTime { get; set; }
        public Vector2 End { get; set; }
        public double EndTime { get; set; }
        public double StartRadius { get; set; }
        public double EndRadius { get; set; }
        public bool IsNested { get; set; }

        public Movement? PreviousMovement { get; set; }
        public Movement? NextMovement { get; set; }

        public double Time => Math.Max(EndTime - StartTime, OsuDifficultyHitObject.MIN_DELTA_TIME);
        public double Distance => (End * ScalingFactor - Start * ScalingFactor).Length;
        public float ScalingFactor => OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(StartRadius, EndRadius);
        public double AbsoluteAngle => Math.Atan2((End - Start).Y, (End - Start).X);

        /// <summary>
        /// Velocity of the cursor as it travels through the note
        /// </summary>
        public double ThroughVelocity { get; set; } = 0;

        public double AimDifficulty { get; set; } = 0;
        public double AimStrain { get; set; } = 0;

        public List<Force> Forces { get; set; } = new List<Force>();

        public override string ToString()
        {
            return $"{Start}->{End} ({Distance:N2}px, {Time:N2}ms)";
        }

        public double Angle(Movement other, bool signed = false)
        {
            Vector2 v1 = other.Start - other.End;
            Vector2 v2 = End - Start;

            float dot = Vector2.Dot(v1, v2);
            float det = v1.X * v2.Y - v1.Y * v2.X;

            double angle = Math.Atan2(det, dot);
            return signed ? angle : Math.Abs(angle);
        }

        /// <summary>
        /// Reevaluates the difficulty and strain of the current movement.
        /// </summary>
        /// <returns>Returns true if the difference between past and current is significant.</returns>
        public bool Reevaluate()
        {
            bool significantDifference = false;

            significantDifference = EvaluateAsFlow();

            return significantDifference;
        }

        protected bool EvaluateAsSnap(double angle = 0, Force? prevForce = null)
        {
            double d = Distance / 2;
            double t = Time / 2;

            double a1 = 2 * d / Math.Pow(t, 2);
            double v1 = a1 * t;
            double a2 = -a1;

            List<Force> forces = new List<Force>();

            forces.Add(new Force()
            {
                Acceleration = a1,
                ForceDuration = t,
                AbsoluteAngle = AbsoluteAngle,
                EndVelocity = v1,
                CursorStart = Start,
                CursorEnd = Start + (End / 2 - Start / 2),
                ScalingFactor = ScalingFactor,
                StartTime = StartTime
            });
            forces.Add(new Force()
            {
                Acceleration = a2,
                ForceDuration = t,
                AbsoluteAngle = AbsoluteAngle,
                StartVelocity = v1,
                StartVelocityAngle = AbsoluteAngle,
                CursorStart = forces[0].CursorEnd,
                CursorEnd = End,
                EndsInClick = !IsNested,
                ScalingFactor = ScalingFactor,
                StartTime = StartTime + forces[0].ForceDuration,
            });

            forces[0].PrevForce = prevForce;
            forces[0].NextForce = forces[1];
            forces[1].PrevForce = forces[0];
            forces[1].NextForce = NextMovement?.Forces.Count > 0 ? NextMovement.Forces[0] : null;

            (double difficulty, double strain) = AimEvaluator.EvaluateForces(forces);

            if (difficulty + strain > AimDifficulty + AimStrain)
            {
                AimDifficulty = difficulty;
                AimStrain = strain;

                Forces = forces;
                if (PreviousMovement?.Forces.Count > 0)
                    PreviousMovement.Forces.Last().NextForce = Forces[0];

                NextMovement?.EvaluateAsSnap(Angle(NextMovement), forces.Last());
            }

            return false;
        }

        protected bool EvaluateAsFlow(Force? lastForce = null)
        {
            double prevExitVelocity;
            double prevExitAngle;

            List<Force> forces = new List<Force>();

            if (lastForce is null)
            {
                prevExitVelocity = (End - Start).Length / Time;
                prevExitAngle = AbsoluteAngle;
            }
            else
            {
                prevExitVelocity = lastForce.EndVelocity;
                prevExitAngle = lastForce.EndVelocityAngle;
            }

            Vector2 displacement = End - Start;

            (double acceleration, double angle, double endVelocity, double endVelocityAngle) =
                GetMovementKinematics(displacement, prevExitVelocity, prevExitAngle, Time);

            forces.Add(new Force()
            {
                Acceleration = acceleration,
                ForceDuration = Time,
                AbsoluteAngle = angle,
                StartVelocity = prevExitVelocity,
                StartVelocityAngle = prevExitAngle,
                EndVelocity = endVelocity,
                EndVelocityAngle = endVelocityAngle,
                CursorStart = Start,
                CursorEnd = End,
                ScalingFactor = 1,
                StartTime = StartTime,
                EndsInClick = true
            });

            forces[0].PrevForce = lastForce;
            if (lastForce != null) lastForce.NextForce = forces[0];

            (double difficulty, double strain) = AimEvaluator.EvaluateForces(forces);

            if (difficulty + strain > AimDifficulty + AimStrain)
            {
                AimDifficulty = difficulty;
                AimStrain = strain;

                Forces = forces;
            }

            return NextMovement?.EvaluateAsFlow(forces.Last()) ?? false;
        }

        public (double AccelationMagnitude, double AccelerationAngle, double EndVelocityMagnitude, double EndVelocityAngle)
            GetMovementKinematics(Vector2 displacement, double v1, double angle, double t)
        {
            // 1. Guard against division by zero
            if (t <= 0) return (0, 0, 0, 0);

            // 2. Initial Velocity Vector (Cartesian)
            Vector2 initialVelocity = new Vector2(
                (float)(v1 * Math.Cos(angle)),
                (float)(v1 * Math.Sin(angle))
            );

            // 4. Calculate Acceleration Vector: a = 2 * (d - v1*t) / t^2
            Vector2 accelVec = 2 * (displacement - (initialVelocity * (float)t)) / (float)(t * t);

            // 5. Calculate Final Velocity Vector: vf = v1 + a*t
            Vector2 finalVelocityVec = initialVelocity + (accelVec * (float)t);

            // 6. Convert to Polar coordinates for the return tuple
            double accelMag = accelVec.Length;
            double accelAng = Math.Atan2(accelVec.Y, accelVec.X);

            double finalVelMag = finalVelocityVec.Length;
            double finalVelAng = Math.Atan2(finalVelocityVec.Y, finalVelocityVec.X);

            return (accelMag, accelAng, finalVelMag, finalVelAng);
        }

        // Useful variables the need to be grabbed safely
        public double LastThroughVelocity => PreviousMovement?.ThroughVelocity ?? 0;
        public double NextDistance => NextMovement?.Distance ?? 0;
        public double NextTime => NextMovement?.NextTime ?? 0;
    }
}
