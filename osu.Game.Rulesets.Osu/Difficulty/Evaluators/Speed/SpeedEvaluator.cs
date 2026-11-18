// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed
{
    public static class SpeedEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner spinner && spinner.SpinsRequired <= 0)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuNextObj = (OsuDifficultyHitObject?)osuCurrObj.Next();

            double deltaTime = osuCurrObj.AdjustedDeltaTime;

            if (osuNextObj != null)
            {
                const double next_delta_influence_on_doubletaps = 0.2;

                // If the current and next object are a doubletappable double then we want to partially combine their delta times to treat it as a single press
                double doubleTapFeasibility = osuCurrObj.CalculateDoubleTapFeasibility(osuNextObj);

                deltaTime += osuNextObj.AdjustedDeltaTime *
                             doubleTapFeasibility *
                             next_delta_influence_on_doubletaps;
            }

            double speedDifficulty = DiffUtils.MillisecondsToBPM(deltaTime) *
                                     calculateSpeedBonus(deltaTime);

            return speedDifficulty;
        }

        private static double calculateSpeedBonus(double deltaTime)
        {
            double strainTime = osuCurrObj.AdjustedDeltaTime;

            // Cap deltatime to the OD 300 hitwindow.
            // 0.93 is derived from making sure 260bpm OD8 streams aren't nerfed harshly, whilst 0.92 limits the effect of the cap.
            strainTime /= Math.Clamp((strainTime / osuCurrObj.HitWindowGreat) / 0.93, 0.92, 1);

            return 1 / strainTime;
        }
    }
}
