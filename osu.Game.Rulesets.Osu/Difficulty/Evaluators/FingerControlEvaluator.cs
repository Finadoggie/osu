// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public class FingerControlEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, double? repeatStrainCount = null)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;

            repeatStrainCount ??= calculateRepeatStrainCount(osuCurrObj);

            double strain = 0.1 / osuCurrObj.AdjustedDeltaTime;

            if (osuCurrObj.BaseObject is Slider)
                strain /= 2.0;

            if (repeatStrainCount % 2 == 0)
                strain = 0;
            else
                strain /= Math.Pow(1.25, (double)repeatStrainCount);

            double doubletapness = 1.0 - osuCurrObj.GetDoubletapness((OsuDifficultyHitObject?)osuCurrObj.Next(0));
            strain *= doubletapness;

            return strain;
        }

        private static double calculateRepeatStrainCount(OsuDifficultyHitObject current)
        {
            throw new NotImplementedException();
        }
    }
}
