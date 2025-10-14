// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public class AltEvaluator
    {
        private static double spacingMidpoint => OsuDifficultyHitObject.NORMALISED_DIAMETER * 1.75; // 1.25 circles distance between centers
        private static double spacingRange => OsuDifficultyHitObject.NORMALISED_DIAMETER * 0.50; // 1.25 circles distance between centers

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            // derive strainTime for calculation
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;

            double currDistance = Math.Max(osuCurrObj.LazyJumpDistance, osuCurrObj.MinimumJumpDistance + (osuPrevObj?.TravelDistance ?? 0));
            double currVelocity = currDistance / osuCurrObj.AdjustedDeltaTime;

            double altStrain = DifficultyCalculationUtils.SmoothstepBellCurve(currDistance, spacingMidpoint, spacingRange);

            // Apply high circle size bonus
            altStrain *= osuCurrObj.SmallCircleBonus;

            return altStrain;
        }
    }
}
