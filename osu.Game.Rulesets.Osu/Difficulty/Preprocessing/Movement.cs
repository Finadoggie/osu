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
        public float ScalingFactor => 1;
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
                StartVelocityAngle = prevForce?.AbsoluteAngle ?? AbsoluteAngle,
                EndVelocity = v1,
                StartPosition = Start,
                EndPosition = Start + (End / 2 - Start / 2),
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
                StartPosition = forces[0].EndPosition,
                EndPosition = End,
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
            }

            NextMovement?.EvaluateAsSnap(Angle(NextMovement), forces.Last());

            return false;
        }

        protected bool EvaluateAsFlow(Force? prevForce = null)
        {
            double prevExitVelocity;
            double prevExitAngle;
            Vector2 prevEndPosition;

            List<Force> forces = new List<Force>();

            if (prevForce is null)
            {
                prevExitVelocity = Distance / Time;
                prevExitAngle = AbsoluteAngle;
                prevEndPosition = Start;
            }
            else
            {
                prevExitVelocity = prevForce.EndVelocity;
                prevExitAngle = prevForce.EndVelocityAngle;
                prevEndPosition = prevForce.EndPosition;
            }

            Vector2 endVelocity;

            if (NextMovement is null)
            {
                endVelocity = End - Start;
            }
            else
            {
                endVelocity = (NextMovement.End - Start) / 2 / (float)Time;
            }

            Vector2 startVelocity = new Vector2
            {
                X = (float)(prevExitVelocity * Math.Cos(prevExitAngle)),
                Y = (float)(prevExitVelocity * Math.Sin(prevExitAngle)),
            };

            AccelerationStep[] steps = CalculateTwoStepTrajectory(startVelocity, endVelocity, Start, End, (float)Time);

            forces.Add(new Force()
            {
                Acceleration = steps[0].Acceleration.Length,
                ForceDuration = steps[0].Duration,
                AbsoluteAngle = GetAngle(steps[0].Acceleration),
                StartPosition = prevEndPosition,
                StartTime = StartTime,
                StartVelocity = prevExitVelocity,
                StartVelocityAngle = prevExitAngle,
                EndPosition = steps[0].EndPosition,
                EndVelocity = steps[0].EndVelocity.Length,
                EndVelocityAngle = GetAngle(steps[0].EndVelocity),
                ScalingFactor = ScalingFactor,
            });
            forces.Add(new Force()
            {
                Acceleration = steps[1].Acceleration.Length,
                ForceDuration = steps[1].Duration,
                AbsoluteAngle = GetAngle(steps[1].Acceleration),
                StartPosition = steps[0].EndPosition,
                StartTime = StartTime + steps[0].Duration,
                StartVelocity = steps[0].EndVelocity.Length,
                StartVelocityAngle = GetAngle(steps[0].EndVelocity),
                EndPosition = steps[1].EndPosition,
                EndVelocity = steps[1].EndVelocity.Length,
                EndVelocityAngle = GetAngle(steps[1].EndVelocity),
                ScalingFactor = ScalingFactor,
                EndsInClick = !IsNested,
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
            }

            return NextMovement?.EvaluateAsFlow(forces.Last()) ?? false;
        }

        public struct AccelerationStep
        {
            public Vector2 Acceleration;
            public float Duration;
            public Vector2 EndPosition;
            public Vector2 EndVelocity;
        }

        public static AccelerationStep[] CalculateTwoStepTrajectory(
            Vector2 startVelocity,
            Vector2 endVelocity,
            Vector2 startPosition,
            Vector2 endPosition,
            float t)
        {
            if (t <= 0f)
            {
                throw new ArgumentException("Time 't' must be strictly greater than zero.", nameof(t));
            }

            // Split the duration evenly into two steps
            float stepDuration = t / 2f;

            // 1. Calculate the first acceleration vector (a0)
            // Formula: a0 = (4 * (p1 - p0) - t * (3 * v0 + v1)) / t^2
            Vector2 positionDelta = endPosition - startPosition;
            Vector2 velocityTerm = (3f * startVelocity) + endVelocity;
            Vector2 a0 = (4f * positionDelta - t * velocityTerm) / (t * t);

            // 2. Calculate the second acceleration vector (a1)
            // Formula: a1 = (2 * (v1 - v0) / t) - a0
            Vector2 a1 = (2f * (endVelocity - startVelocity) / t) - a0;

            // 3. Calculate the intermediate state (end of step 1)
            Vector2 midVelocity = startVelocity + (a0 * stepDuration);
            Vector2 midPosition = startPosition + (startVelocity * stepDuration) + (0.5f * a0 * stepDuration * stepDuration);

            // 4. Construct and return the two steps
            return new AccelerationStep[]
            {
                new AccelerationStep
                {
                    Acceleration = a0,
                    Duration = stepDuration,
                    EndPosition = midPosition,
                    EndVelocity = midVelocity
                },
                new AccelerationStep
                {
                    Acceleration = a1,
                    Duration = stepDuration,
                    // We use the exact target parameters here to avoid tiny floating-point inaccuracies
                    EndPosition = endPosition,
                    EndVelocity = endVelocity
                }
            };
        }

        public double GetAngle(Vector2 v) => Math.Atan2(v.Y, v.X);

        // Useful variables the need to be grabbed safely
        public double LastThroughVelocity => PreviousMovement?.ThroughVelocity ?? 0;
        public double NextDistance => NextMovement?.Distance ?? 0;
        public double NextTime => NextMovement?.NextTime ?? 0;
    }
}
