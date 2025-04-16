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
        private const double timing_threshold_const = 107;
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

            double timing_threshold = 107;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastLastObj = (OsuDifficultyHitObject)current.Previous(1);

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            double jumpDistance = osuCurrObj.LazyJumpDistance;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (osuLastObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuLastObj.TravelDistance / osuLastObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / osuCurrObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                double sliderDistance = (movementVelocity + travelVelocity) * osuCurrObj.StrainTime;

                jumpDistance = Math.Max(jumpDistance, sliderDistance);
            }

            double prevJumpDistance = osuLastObj.LazyJumpDistance;

            // Do the same for the previous object
            if (osuLastLastObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuLastLastObj.TravelDistance / osuLastLastObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuLastObj.MinimumJumpDistance / osuLastObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                double sliderDistance = (movementVelocity + travelVelocity) * osuLastObj.StrainTime;

                prevJumpDistance = Math.Max(prevJumpDistance, sliderDistance);
            }

            double result = 0;

            if (osuCurrObj.Angle != null && osuCurrObj.Angle.Value > angle_bonus_begin)
            {
                const double scale = 90;

                double angleBonus = Math.Sqrt(
                    Math.Max(jumpDistance - scale, 0)
                    * Math.Max(prevJumpDistance - scale, 0)
                    * Math.Pow(Math.Sin(osuCurrObj.Angle.Value - angle_bonus_begin), 2)
                );
                result = 1.5 * applyDiminishingExp(Math.Max(0, angleBonus)) / Math.Max(timing_threshold, osuLastObj.StrainTime);

                // Nerf if rhythms are not the same
                // double rhythmRatio = Math.Max(osuCurrObj.StrainTime, osuLastObj.StrainTime) / Math.Min(osuCurrObj.StrainTime, osuLastObj.StrainTime);
                // result /= Math.Pow(rhythmRatio, 0.25);
            }

            double jumpDistanceExp = applyDiminishingExp(jumpDistance);

            double sliderBonus = 0;

            if (osuLastObj.BaseObject is Slider && withSliderTravelDistance)
            {
                // Reward sliders based on velocity.
                sliderBonus = applyDiminishingExp(osuLastObj.TravelDistance) / osuLastObj.TravelTime;
                sliderBonus *= slider_multiplier;
            }

            return Math.Max(
                result + jumpDistanceExp / Math.Max(osuCurrObj.StrainTime, timing_threshold),
                jumpDistanceExp / osuCurrObj.StrainTime
            ) + sliderBonus;
        }

        private static double applyDiminishingExp(double val) => Math.Pow(val, 0.99);
    }
}
