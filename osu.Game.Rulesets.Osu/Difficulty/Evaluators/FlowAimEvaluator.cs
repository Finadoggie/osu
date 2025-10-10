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
            var osuPrev3Obj = (OsuDifficultyHitObject)current.Previous(1);

            double currDistanceDifference = Math.Abs(osuCurrObj.LazyJumpDistance + osuPrevObj.TravelDistance - osuPrevObj.LazyJumpDistance + osuPrev2Obj.TravelDistance);
            double prevDistanceDifference = Math.Abs(osuPrevObj.LazyJumpDistance + osuPrev2Obj.TravelDistance - osuPrev2Obj.LazyJumpDistance + osuPrev3Obj.TravelDistance);

            double jerk = Math.Abs(currDistanceDifference - prevDistanceDifference);

            double angleDifferenceAdjusted = Math.Sin(directionChange(current) / 2) * 180;

            double acuteBonus = 0;

            if (osuCurrObj.Angle.IsNotNull())
            {
                acuteBonus = calcAcuteAngleBonus(osuCurrObj.Angle.Value) * 4;

                // Nerf the third note of bursts as its angle is not representative of its flow difficulty
                if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuPrev2Obj.AdjustedDeltaTime) > 25)
                {
                    angleDifferenceAdjusted *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(180), double.DegreesToRadians(90));
                    jerk *= DifficultyCalculationUtils.Smootherstep(osuCurrObj.Angle.Value, double.DegreesToRadians(180), double.DegreesToRadians(90));
                }
            }

            double angularChangeBonus = Math.Max(0.0, 1.3 * Math.Log10(angleDifferenceAdjusted));

            double adjustedDistanceScale = 0.85 + Math.Min(1, jerk / 15) + Math.Max(angularChangeBonus, acuteBonus) * Math.Clamp(jerk / 30, 0.3, 1);

            // Value distance exponentially, and scale with direction and distance changes
            double distanceFactor = Math.Pow(osuCurrObj.LazyJumpDistance + osuPrevObj.TravelDistance, 2) * adjustedDistanceScale;

            double difficulty = distanceFactor / osuCurrObj.AdjustedDeltaTime;

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 0.14;
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

            return DifficultyCalculationUtils.Smootherstep(perpendicularDistance, OsuDifficultyHitObject.NORMALISED_RADIUS * 0.5, OsuDifficultyHitObject.NORMALISED_RADIUS * 1.5);
        }

        private static double calcAcuteAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(70));
    }
}
