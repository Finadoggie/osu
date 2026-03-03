// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
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
        public double Distance => (End - Start).Length;
        public float ScalingFactor => 1;
        public double AbsoluteAngle => Math.Atan2((End - Start).Y, (End - Start).X);

        /// <summary>
        /// Velocity of the cursor as it travels through the note
        /// </summary>
        public double ThroughVelocity { get; set; } = 0;

        public double AimDifficulty { get; set; } = 0;
        public double AimStrain { get; set; } = 0;

        public IReadOnlyList<Force>? Forces { get; set; }

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
        public void Reevaluate()
        {
            Force? previousForce = PreviousMovement?.Forces?.Last() ?? null;

            const int num_divisions = 4;
            const int num_types = 5;

            List<IReadOnlyList<Force>> forcesSet = new List<IReadOnlyList<Force>>();

            for (int i = 0; i < num_types; i++)
            {
                for (int j = 0; j < num_divisions; j++)
                {
                    Force[]? newForce = CreateForces(1.0f / (num_divisions) * (j + 1), i, previousForce);
                    if (newForce is not null) forcesSet.Add((Force[])newForce);
                }
            }

            IReadOnlyList<Force> bestSet = forcesSet.MinBy(forces =>
            {
                (double difficulty, double strain) = AimEvaluator.EvaluateForces(forces);
                return difficulty + strain;
            }) ?? CreateForces(0.0f, 0, previousForce)!; // Fallback, should never happen in practice

            Forces = bestSet;
            if (previousForce != null) previousForce.NextForce = Forces[0];

            (AimDifficulty, AimStrain) = AimEvaluator.EvaluateForces(bestSet);
        }

        protected Force[]? CreateForces(float flowPercent, int type, Force? prevForce = null)
        {
            double prevExitVelocity;
            double prevExitAngle;
            Vector2 prevEndPosition;
            Vector2 endPosition = End;
            float assumedRadius = (float)EndRadius;

            List<Force> forces = new List<Force>();

            if (prevForce is null)
            {
                prevExitVelocity = 0;
                prevExitAngle = 0;
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
                endVelocity = Vector2.Zero;
            }
            else
            {
                // Match next angle
                if (type == 1) endVelocity = (NextMovement.End - NextMovement.Start) / (float)Time * (flowPercent);
                // Avg angles
                else endVelocity = (NextMovement.End - prevEndPosition) / 2 / (float)Time * (flowPercent);

                // Cheese
                if (type == 2 || type == 3 || type == 4)
                {
                    float cheese_percent;

                    if (type == 3) cheese_percent = 0.33f;
                    else if (type == 4) cheese_percent = 0.67f;
                    else cheese_percent = 1;

                    Vector2 offset = (End - prevEndPosition - (NextMovement.End - prevEndPosition) / 2) / 2 * cheese_percent;
                    float length = Math.Min(offset.Length, (float)EndRadius * cheese_percent * 0.9f);

                    offset = new Vector2
                    {
                        X = length * (float)Math.Cos(GetAngle(offset)),
                        Y = length * (float)Math.Sin(GetAngle(offset)),
                    };

                    endPosition = End - offset;

                    assumedRadius = (float)EndRadius * (float)(1 - Math.Pow(length / EndRadius, 2.00));
                }
            }

            Vector2 startVelocity = new Vector2
            {
                X = (float)(prevExitVelocity * Math.Cos(prevExitAngle)),
                Y = (float)(prevExitVelocity * Math.Sin(prevExitAngle)),
            };

            AccelerationStep[] steps = CalculateTwoStepTrajectory(startVelocity, endVelocity, prevEndPosition, endPosition, (float)Time);

            string label = Math.Round(flowPercent * 100).ToString(CultureInfo.InvariantCulture) + "%" + "\t Type " + type;

            forces.Add(new Force
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
                AssumedRadius = assumedRadius,
                AimType = label,
                Parent = this,
            });
            forces.Add(new Force
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
                AssumedRadius = assumedRadius,
                EndsInClick = !IsNested,
                AimType = label,
                Parent = this,
            });

            forces[0].PrevForce = prevForce;
            forces[0].NextForce = forces[1];
            forces[1].PrevForce = forces[0];

            return forces.ToArray();
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
