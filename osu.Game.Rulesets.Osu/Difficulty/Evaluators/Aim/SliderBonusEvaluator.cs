// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class SliderBonusEvaluator
    {
        /// <summary>
        /// Evaluates the slider bonus.
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (!(withSliderTravelDistance && current.BaseObject is Slider))
                return 0;

            OsuDifficultyHitObject curr = (OsuDifficultyHitObject)current;

            return calculateSliderBonus(curr);
        }

        private static double calculateSliderBonus(OsuDifficultyHitObject current)
            => current.TravelDistance / current.TravelTime;
    }
}
