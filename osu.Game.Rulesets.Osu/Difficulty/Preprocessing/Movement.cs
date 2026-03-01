// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        public double Distance => (End * (OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(StartRadius, EndRadius)) - Start * (OsuDifficultyHitObject.NORMALISED_RADIUS / (float)Math.Max(EndRadius, StartRadius))).Length;

        public double ExitVelocity { get; private set; } = 0;

        public double AimDifficulty { get; private set; } = 0;
        public double AimStrain { get; private set; } = 0;

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

            double[] forces = [];

            significantDifference = reevaluateAimDifficultyWith(forces) || significantDifference;

            return significantDifference;
        }

        private bool reevaluateAimDifficultyWith(double[] forces)
        {
            (double newDifficulty, double newStrain) = AimEvaluator.EvaluateForces(forces);

            if (newDifficulty + newStrain > AimDifficulty + AimStrain)
            {
                AimDifficulty = newDifficulty;
                AimStrain = newStrain;
                return true;
            }

            return false;
        }

        private static double getForce(double startVelocity, double endVelocity, double time) => (endVelocity - startVelocity) / time;
    }
}
