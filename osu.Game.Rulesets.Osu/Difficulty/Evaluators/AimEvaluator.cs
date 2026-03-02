// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double wide_angle_multiplier = 3.0;
        private const double acute_angle_multiplier = 2.3;
        private const double slider_multiplier = 1.5;
        private const double velocity_change_multiplier = 0.75;
        private const double wiggle_multiplier = 1.02; // WARNING: Increasing this multiplier beyond 1.02 reduces difficulty as distance increases. Refer to the desmos link above the wiggle bonus calculation
        private const double nested_movement_multiplier = 7.0;

        public static (double difficulty, double strain) EvaluateForces(IReadOnlyList<Force> forces)
        {
            double currentDifficulty = 0;
            double currentStrain = 0;

            foreach (var force in forces)
            {
                double currVelocity = force.Acceleration * force.ForceDuration;
                currVelocity = Math.Abs(currVelocity);

                double baseStrain = currVelocity;
                double wideAngleBonus = 0;

                double scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / force.AssumedRadius;

                double? angle = force.Angle();

                if (angle != null)
                {
                    wideAngleBonus = calcWideAngleBonus(angle.Value);
                    wideAngleBonus *= currVelocity;
                }

                // Add to running total
                if (force.EndsInClick)
                {
                    // double window = CalculateTimeOverCircle(force, force.EndPosition, OsuDifficultyHitObject.NORMALISED_RADIUS);
                    //
                    // if (window == 0) window = double.PositiveInfinity;
                    // currentDifficulty = 50 / window;

                    currentDifficulty = force.EndVelocity * scalingFactor;
                }

                // baseStrain += wideAngleBonus * wide_angle_multiplier;

                currentStrain += baseStrain * scalingFactor;
            }

            double followupStrain = 0;

            // Get strain to reach next object
            if (forces.Last().Parent.NextMovement is not null)
            {
                Force lastForce = forces.Last();
                Movement next = lastForce.Parent.NextMovement!;
                Vector2 displacement = next.End - next.Start;

                followupStrain = AccelerateToNextObject(displacement, lastForce.EndVelocity, lastForce.EndVelocityAngle, next.Time) * next.Time;

                double scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / lastForce.AssumedRadius;
                followupStrain *= scalingFactor;
            }

            currentStrain *= 2;
            followupStrain *= 2;
            currentDifficulty *= 2;

            return (currentDifficulty, currentStrain + followupStrain);
        }

        public static double
            AccelerateToNextObject(Vector2 displacement, double v1, double angle, double t)
        {
            // 1. Guard against division by zero
            if (t <= 0) return 0;

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

            return accelMag;
        }

        public static double CalculateTimeOverCircle(Force? startForce, Vector2 circleCenter, double radius)
        {
            double? entryTime = null;
            double? exitTime = null;

            Force? current = startForce;

            while (current != null)
            {
                // 1. Get the path parameters for this segment
                double cos = Math.Cos(current.AbsoluteAngle);
                double sin = Math.Sin(current.AbsoluteAngle);
                Vector2 dir = new Vector2((float)cos, (float)sin);
                Vector2 startToCenter = circleCenter - current.StartPosition;

                // 2. Project center onto the line to find closest approach
                double projection = Vector2.Dot(startToCenter, dir);
                double distSq = startToCenter.LengthSquared - (projection * projection);

                // If the line of this force segment passes through the circle
                if (distSq <= radius * radius)
                {
                    double offset = Math.Sqrt(radius * radius - distSq);
                    double sEntry = projection - offset;
                    double sExit = projection + offset;

                    // 3. Convert distances (s) to local times (t) within this segment
                    double? tEntry = solveQuadratic(0.5 * current.Acceleration, current.StartVelocity, -sEntry, current.ForceDuration);
                    double? tExit = solveQuadratic(0.5 * current.Acceleration, current.StartVelocity, -sExit, current.ForceDuration);

                    // 4. Handle Entry
                    if (entryTime == null)
                    {
                        if (tEntry.HasValue)
                            entryTime = current.StartTime + tEntry.Value;
                        else if (isPointInside(current.StartPosition, circleCenter, radius))
                            entryTime = current.StartTime; // Already inside at segment start
                    }

                    // 5. Handle Exit
                    if (entryTime != null && exitTime == null)
                    {
                        if (tExit.HasValue)
                        {
                            exitTime = current.StartTime + tExit.Value;
                            break; // We found both!
                        }
                    }
                }

                // If we finish a segment while inside and have no exit yet,
                // we continue to the next Force.
                current = current.NextForce;
            }

            if (entryTime.HasValue && exitTime.HasValue)
            {
                return exitTime.Value - entryTime.Value;
            }

            return 0; // Return 0 if no complete entrance and exit were found
        }

        private static bool isPointInside(Vector2 point, Vector2 center, double radius)
        {
            return Vector2.DistanceSquared(point, center) <= radius * radius;
        }

        private static double? solveQuadratic(double a, double b, double c, double maxT)
        {
            if (Math.Abs(a) < 1e-9) // Linear motion (no acceleration)
            {
                if (Math.Abs(b) < 1e-9) return null;

                double t = -c / b;
                return (t >= 0 && t <= maxT) ? t : null;
            }

            double discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return null;

            double sqrtD = Math.Sqrt(discriminant);
            double t1 = (-b - sqrtD) / (2 * a);
            double t2 = (-b + sqrtD) / (2 * a);

            // We want the smallest non-negative time within the segment window
            if (t1 >= 0 && t1 <= maxT) return t1;
            if (t2 >= 0 && t2 <= maxT) return t2;

            return null;
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));
    }
}
