// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class AgilityEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of fast aiming
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            const double previous_delta_influence = 0.5;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject?)current.Previous();

            // Assume that the player always uses at least the full great hitwindow for the movement
            double currentDelta = Math.Max(osuCurrObj.AdjustedDeltaTime, osuCurrObj.HitWindowGreat);

            // For objects that are stacked we want to reduce the agility difficulty slightly by combining delta times of both objects together
            // Because we can assume that they likely would be done in one movement.
            double previousDelta = 0;

            if (osuPrevObj != null)
            {
                previousDelta = osuPrevObj.AdjustedDeltaTime *
                                DiffUtils.ReverseLerp(osuPrevObj.LazyJumpDistance, OsuDifficultyHitObject.NORMALISED_RADIUS, 0);
            }

            double combinedDelta = currentDelta + previousDelta * previous_delta_influence;

            double angleSwitchingBonus = calculateAngleSwitchingBonus(osuCurrObj, osuPrevObj);

            double kwotangular = osuPrevObj is not null ? vectorAngleRepetition(osuCurrObj, osuPrevObj) : 1;

            double agilityDifficulty = (1 + angleSwitchingBonus) * 4_500_000 / DiffUtils.Pow(combinedDelta, 2.8) * kwotangular;

            agilityDifficulty *= osuCurrObj.SmallCircleBonus;

            return agilityDifficulty;
        }

        private static double calculateAngleSwitchingBonus(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject? osuPrevObj)
        {
            const double angle_switching_bonus = 0.8;

            if (osuCurrObj.Angle == null || osuPrevObj?.Angle == null)
                return 0;

            double acutenessDifference = 1 - Math.Min(AngleUtils.CalculateAcuteness(osuCurrObj.Angle.Value), DiffUtils.Pow(AngleUtils.CalculateAcuteness(osuPrevObj.Angle.Value), 3));

            double rhythmDifferencePenalty = DiffUtils.Pow(Math.Min(osuCurrObj.AdjustedDeltaTime, osuPrevObj.AdjustedDeltaTime) / Math.Max(osuCurrObj.AdjustedDeltaTime, osuPrevObj.AdjustedDeltaTime),
                3);

            return acutenessDifference *
                   rhythmDifferencePenalty *
                   DiffUtils.ReverseLerp(osuPrevObj.LazyJumpDistance, OsuDifficultyHitObject.NORMALISED_RADIUS, OsuDifficultyHitObject.NORMALISED_DIAMETER) *
                   angle_switching_bonus;
        }

        private static double vectorAngleRepetition(OsuDifficultyHitObject current, OsuDifficultyHitObject previous)
        {
            if (
                current.Angle == null ||
                previous.Angle == null ||
                previous.BaseObject is Spinner ||
                previous.Previous(0).BaseObject is Spinner)
                return 1;

            const double note_limit = 6;
            const double maximum_repetition_nerf = 0.15;
            const double maximum_vector_influence = 0.5;

            double constantAngleCount = 0;

            for (int index = 0; index < note_limit; index++)
            {
                OsuDifficultyHitObject? prevObj = (OsuDifficultyHitObject)current.Previous(index);

                if (prevObj == null || prevObj.BaseObject is Spinner)
                    break;

                // Only consider vectors in the same jump section, stopping to change rhythm ruins momentum
                if (Math.Max(current.AdjustedDeltaTime, prevObj.AdjustedDeltaTime) > 1.1 * Math.Min(current.AdjustedDeltaTime, prevObj.AdjustedDeltaTime))
                    break;

                if (prevObj.NormalisedVectorAngle == null || current.NormalisedVectorAngle == null)
                    continue;

                double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - prevObj.NormalisedVectorAngle.Value);

                // Refer to this desmos for tuning, constants need to be precise so that values stay within the range of 0 and 1.
                // https://www.desmos.com/calculator/a8jesv5sv2
                constantAngleCount += Math.Cos(8 * Math.Min(double.DegreesToRadians(11.25), angleDifference));
            }

            double vectorRepetition = DiffUtils.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);

            double stackFactor = DiffUtils.Smootherstep(current.LazyJumpDistance, 0, OsuDifficultyHitObject.NORMALISED_DIAMETER);

            double currAngle = current.Angle.Value;
            double lastAngle = previous.Angle.Value;

            double angleDifferenceAdjusted = Math.Cos(2 * Math.Min(double.DegreesToRadians(45), Math.Abs(currAngle - lastAngle) * stackFactor));

            double baseNerf = 1 - maximum_repetition_nerf * AngleUtils.CalculateAcuteness(lastAngle) * angleDifferenceAdjusted;

            return DiffUtils.Pow(baseNerf + (1 - baseNerf) * vectorRepetition * maximum_vector_influence * stackFactor, 2);
        }
    }
}
