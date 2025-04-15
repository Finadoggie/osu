// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double angle_bonus_begin = Math.PI / 3;
        private const double timing_threshold = 107;
        private const double slider_multiplier = 1.35;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);

            double result = 0;
            if (osuCurrObj.Angle != null && osuCurrObj.Angle.Value > angle_bonus_begin)
            {
                const double scale = 90;

                double angleBonus = Math.Sqrt(
                    Math.Max(osuLastObj.LazyJumpDistance - scale, 0)
                    * Math.Pow(Math.Sin(osuCurrObj.Angle.Value - angle_bonus_begin), 2)
                    * Math.Max(osuCurrObj.LazyJumpDistance - scale, 0));
                result = 1.5 * applyDiminishingExp(Math.Max(0, angleBonus)) / Math.Max(timing_threshold, osuLastObj.StrainTime);
            }

            double jumpDistanceExp = applyDiminishingExp(osuCurrObj.LazyJumpDistance);
            double travelDistanceExp = applyDiminishingExp(osuCurrObj.TravelDistance);

            return Math.Max(
                result + (jumpDistanceExp + travelDistanceExp + Math.Sqrt(travelDistanceExp * jumpDistanceExp)) / Math.Max(osuCurrObj.StrainTime, timing_threshold),
                (Math.Sqrt(travelDistanceExp * jumpDistanceExp) + jumpDistanceExp + travelDistanceExp) / osuCurrObj.StrainTime);
        }

        private static double applyDiminishingExp(double val) => Math.Pow(val, 0.99);
    }
}
