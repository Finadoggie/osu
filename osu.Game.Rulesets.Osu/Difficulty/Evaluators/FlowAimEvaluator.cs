// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrev2Obj = (OsuDifficultyHitObject)current.Previous(1);

            double currDistance = osuCurrObj.LazyJumpDistance + osuPrevObj.TravelDistance;
            double currTime = osuCurrObj.AdjustedDeltaTime;
            double currVelocity = currDistance / currTime;

            double prevDistance = osuPrevObj.LazyJumpDistance + osuPrev2Obj.TravelDistance;
            double prevTime = osuPrevObj.AdjustedDeltaTime;
            double prevVelocity = prevDistance / prevTime;


            double difficulty = currVelocity;

            double angleBonus = 0;

            double angle = osuCurrObj.Angle ?? Math.PI;

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                // Assume player cursor follows a circle
                double circularVelocity = calculateCircularCursorPathDistance(angle, currDistance) / currTime;
                double distRatio = Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity);
                angleBonus = (circularVelocity - currVelocity) * distRatio;
            }

            difficulty += angleBonus; // If a multiplier is ever applied to angleBonus, it must be between 0 and 1

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 0.225;
        }

        private static double directionChange(DifficultyHitObject current)
        {
            double directionChangeFactor = 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            if (osuCurrObj.AngleSigned.IsNull() || osuPrevObj.AngleSigned.IsNull() ||
                osuCurrObj.Angle.IsNull() || osuPrevObj.Angle.IsNull()) return directionChangeFactor;

            double signedAngleDifference = Math.Abs(osuCurrObj.AngleSigned.Value - osuPrevObj.AngleSigned.Value);

            // Account for the fact that you can aim patterns in a straight line
            signedAngleDifference *= calculateLinearity(osuCurrObj);

            double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);

            directionChangeFactor += Math.Max(signedAngleDifference, angleDifference);

            double acuteBonus = calcAcuteAngleBonus(osuCurrObj.Angle.Value) * 3 * DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance,OsuDifficultyHitObject.NORMALISED_RADIUS, OsuDifficultyHitObject.NORMALISED_RADIUS * 3);

            var osuPrev2Obj = (OsuDifficultyHitObject)current.Previous(1);
            if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrevObj.AdjustedDeltaTime) > 25 ||
                Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrev2Obj.AdjustedDeltaTime) > 25)
                return acuteBonus;

            directionChangeFactor = Math.Max(directionChangeFactor, acuteBonus);

            return directionChangeFactor;
        }

        private static double calculateLinearity(OsuDifficultyHitObject current)
        {
            var curBaseObj = (OsuHitObject)current.BaseObject;
            var prevBaseObj = (OsuHitObject)current.Previous(0).BaseObject;
            var prev2BaseObj = (OsuHitObject)current.Previous(1).BaseObject;

            Vector2 lineVector = prev2BaseObj.StackedEndPosition - curBaseObj.StackedEndPosition;
            Vector2 toMiddle = prevBaseObj.StackedEndPosition - curBaseObj.StackedEndPosition;

            float dotToMiddleLine = Vector2.Dot(toMiddle, lineVector);
            float dotLineLine = Vector2.Dot(lineVector, lineVector);

            float projectionScalar = dotToMiddleLine / dotLineLine;

            Vector2 projection = lineVector * projectionScalar;

            float scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / (float)curBaseObj.Radius;

            double perpendicularDistance = curBaseObj.StackedPosition.Equals(prev2BaseObj.StackedPosition)
                ? current.LazyJumpDistance
                : (toMiddle * scalingFactor - projection * scalingFactor).Length;

            return DifficultyCalculationUtils.Smootherstep(perpendicularDistance, OsuDifficultyHitObject.NORMALISED_RADIUS, OsuDifficultyHitObject.NORMALISED_RADIUS * 1.5);
        }

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(70));

        private static double calculateCircularCursorPathDistance(double angle, double distance)
        {
            // Case: Straight line
            if (angle >= Math.PI)
                return distance;

            // Case: Straight back
            if (angle <= 0)
                return Math.PI * distance; // Distance = diameter in this case

            // Case: Everything else
            // Calculate radius of circle that cursor is assumed to follow
            double a = Math.Cos(angle) * distance;
            double b = Math.Sin(angle) * distance;
            double q = distance / 2;
            double p = distance / 2 * Math.Tan(angle / 2);
            double r = Math.Sqrt(Math.Pow(q - a, 2) + Math.Pow(p - b, 2));

            // Return segment of that circle encompassing the distance the cursor follows
            return (1 - angle / Math.PI) * 2 * Math.PI * r;
        }
    }
}
